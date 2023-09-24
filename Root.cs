using Godot;
using Flecs.NET.Core;

public partial class Root : Node2D
{
	[Export] public int EntityCount { get; set; } = 1000;
	[Export] public int VelocityMin { get; set; } = 50;
	[Export] public int VelocityMax { get; set; } = 500;
	[Export] public int ScaleMin { get; set; } = 5;
	[Export] public int ScaleMax { get; set; } = 15;

	public World World;

	public override void _Ready()
	{
		World = World.Create();
		World.Set<Root>(this);
		World.Import<Components>();
		World.Import<Systems>();

		for (int i = 0; i < EntityCount; i++)
		{
			Vector2 position = new(GD.Randf() * GetViewportRect().Size.X, GD.Randf() * GetViewportRect().Size.Y);
			Vector2 velocity = Vector2.FromAngle(GD.Randf() * 2 * Mathf.Pi) * GD.RandRange(VelocityMin, VelocityMax);
			Vector2 scale = Vector2.One * GD.RandRange(ScaleMin, ScaleMax);
			Color color = new(GD.Randf(), GD.Randf(), GD.Randf());

			World.Entity()
				.Set<Vector2>(Components.Position, position)
				.Set<Vector2>(Components.Velocity, velocity)
				.Set<Vector2>(Components.Scale, scale)
				.Set<Color>(color);
		}
	}

	public override void _Process(double delta)
	{
		World.Progress((float)delta);
	}
}

public struct Components : IFlecsModule
{
	public static Entity Position;
	public static Entity Velocity;
	public static Entity Scale;

	public void InitModule(ref World world)
	{
		world.Module<Components>();

		Position = world.Entity();
		Velocity = world.Entity();
		Scale = world.Entity();

		InitMultiMeshInstance(ref world);
	}

	private static void InitMultiMeshInstance(ref World world)
	{
		Root root = world.Get<Root>();

		QuadMesh quad = new();
		quad.Size = new Vector2(1, 1);

		MultiMesh multiMesh = new();
		multiMesh.TransformFormat = MultiMesh.TransformFormatEnum.Transform2D;
		multiMesh.UseColors = true;
		multiMesh.Mesh = quad;
		multiMesh.InstanceCount = root.EntityCount;
		multiMesh.VisibleInstanceCount = root.EntityCount;

		MultiMeshInstance2D multiMeshInstance = new();
		multiMeshInstance.Texture = new Texture2D();
		multiMeshInstance.Multimesh = multiMesh;

		world.Set<MultiMeshInstance2D>(multiMeshInstance);
		root.AddChild(multiMeshInstance);
	}
}

public struct Systems : IFlecsModule
{
	public void InitModule(ref World world)
	{
		world.Import<Components>();
		world.Module<Systems>();

		world.Routine(
			name: "Update Position",
			filter: world.FilterBuilder()
				.With<Vector2>(Components.Position)
				.With<Vector2>(Components.Velocity),
			callback: (Iter it) =>
			{
				Column<Vector2> position = it.Field<Vector2>(1);
				Column<Vector2> velocity = it.Field<Vector2>(2);

				Rect2 screen = it.World().Get<Root>().GetViewportRect();

				foreach (int i in it)
				{
					position[i] += velocity[i] * it.DeltaTime();

					if (position[i].X >= screen.Size.X)
						velocity[i].X = -Mathf.Abs(velocity[i].X);

					if (position[i].Y >= screen.Size.Y)
						velocity[i].Y = -Mathf.Abs(velocity[i].Y);

					if (position[i].X <= 0)
						velocity[i].X = Mathf.Abs(velocity[i].X);

					if (position[i].Y <= 0)
						velocity[i].Y = Mathf.Abs(velocity[i].Y);
				}
			}
		);

		world.Routine(
			name: "Update Colors",
			filter: world.FilterBuilder().With<Color>(),
			callback: (Iter it) =>
			{
				Column<Color> color = it.Field<Color>(1);

				foreach (int i in it)
					color[i].H += it.DeltaTime();
			}
		);

		world.Routine(
			name: "Upload Squares",
			filter: world.FilterBuilder()
				.With<Vector2>(Components.Position)
				.With<Vector2>(Components.Scale)
				.With<Color>(),
			callback: (Iter it) =>
			{
				Column<Vector2> position = it.Field<Vector2>(1);
				Column<Vector2> scale = it.Field<Vector2>(2);
				Column<Color> color = it.Field<Color>(3);

				MultiMeshInstance2D multiMeshInstance = it.World().Get<MultiMeshInstance2D>();
				MultiMesh multiMesh = multiMeshInstance.Multimesh;

				foreach (int i in it)
				{
					multiMesh.SetInstanceTransform2D(i, new Transform2D(0, scale[i], 0, position[i]));
					multiMesh.SetInstanceColor(i, color[i]);
				}
			}
		);
	}
}
