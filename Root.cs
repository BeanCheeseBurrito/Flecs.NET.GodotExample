using System.Globalization;
using Godot;
using Flecs.NET.Core;

namespace FlecsExample;

// Create world, import modules, and run the main loop.
public partial class Root : Node2D
{
	public World World;

	public override void _Ready()
	{
		World = World.Create();								 // Create world.
		World.Set(this);									 // Set root node as a singleton.
		World.SetThreads(System.Environment.ProcessorCount); // Enable multi-threaded systems.
		World.Import<GameComponents>();						 // Import components.
		World.Import<GameSystems>();						 // Import systems.

		// Create entities with random values.
		for (int i = 0; i < 3000; i++)
		{
			Vector2 position = new Vector2(GD.RandRange(0, 1000), GD.RandRange(0, 1000));
			Vector2 velocity = new Vector2(GD.Randf(), GD.Randf()).Normalized() * GD.RandRange(50, 500);
			Vector2 scale = Vector2.One * GD.RandRange(5, 15);
			Color color = new Color(GD.Randf(), GD.Randf(), GD.Randf());

			World.Entity("e" + i)
				.SetFirst<Vector2, Position>(position)
				.SetFirst<Vector2, Velocity>(velocity)
				.SetFirst<Vector2, Scale>(scale)
				.Set(color);
		}
	}

	public override void _Process(double delta)
	{
		World.Progress((float)delta);
	}
}

// Class to hold reference to multi mesh.
// Managed types are supported but shouldn't be used in performance critical systems.
public class RenderData
{
	public MultiMeshInstance2D MultiMeshInstance;
	public MultiMesh MultiMesh;
}

// Tags
public struct Position { }
public struct Velocity { }
public struct Scale { }

public struct GameComponents : IFlecsModule
{
	public void InitModule(ref World world)
	{
		MultiMeshInstance2D multiMeshInstance = CreateMultiMeshInstance();

		// Set module scope that entities and components, and systems will be registered under.
		world.Module<GameComponents>();

		world.Component<RenderData>();
		world.Component<Position>();
		world.Component<Velocity>();
		world.Component<Scale>();

		// Set as a singleton so we can use the multi mesh in systems.
		world.Set(new RenderData
		{
			MultiMeshInstance = multiMeshInstance,
			MultiMesh = multiMeshInstance.Multimesh
		});

		// Multi mesh instance needs to be added to the scene tree.
		world.Get<Root>().AddChild(multiMeshInstance);
	}

	// Create a multi mesh so render instanced squares.
	private static MultiMeshInstance2D CreateMultiMeshInstance()
	{
		QuadMesh quad = new();
		quad.Size = new Vector2(1, 1);

		MultiMesh multiMesh = new();
		multiMesh.TransformFormat = MultiMesh.TransformFormatEnum.Transform2D;
		multiMesh.UseColors = true;
		multiMesh.Mesh = quad;

		MultiMeshInstance2D multiMeshInstance = new();
		multiMeshInstance.Texture = new Texture2D();
		multiMeshInstance.Multimesh = multiMesh;

		return multiMeshInstance;
	}
}

public struct GameSystems : IFlecsModule
{
	public void InitModule(ref World world)
	{
		world.Import<GameComponents>();
		world.Module<GameSystems>();

		world.Routine(
			name: "Fps Counter",
			routine: world.RoutineBuilder().Interval(.5f),
			callback: (Iter _) =>
			{
				GD.Print(
					Performance.GetMonitor(Performance.Monitor.TimeFps)
						.ToString(CultureInfo.InvariantCulture)
				);
			}
		);

		world.Routine(
			name: "Move",
			filter: world.FilterBuilder()
				.With<Vector2, Position>()
				.With<Vector2, Velocity>(),
			routine: world.RoutineBuilder().MultiThreaded(),
			callback: it =>
			{
				Column<Vector2> position = it.Field<Vector2>(1);
				Column<Vector2> velocity = it.Field<Vector2>(2);

				foreach (int i in it)
					position[i] += velocity[i] * it.DeltaTime();
			}
		);

		world.Routine(
			name: "Bounce",
			filter: world.FilterBuilder()
				.With<Vector2, Position>()
				.With<Vector2, Velocity>(),
			routine: world.RoutineBuilder().MultiThreaded(),
			callback: it =>
			{
				Column<Vector2> position = it.Field<Vector2>(1);
				Column<Vector2> velocity = it.Field<Vector2>(2);

				foreach (int i in it)
				{
					if (position[i].X >= 0 + 1200)
						velocity[i].X = -Mathf.Abs(velocity[i].X);

					if (position[i].Y >= 0 + 720)
						velocity[i].Y = -Mathf.Abs(velocity[i].Y);

					if (position[i].X <= 0)
						velocity[i].X = Mathf.Abs(velocity[i].X);

					if (position[i].Y <= 0)
						velocity[i].Y = Mathf.Abs(velocity[i].Y);
				}
			}
		);

		world.Routine(
			name: "Color",
			filter: world.FilterBuilder()
				.With<Color>()
				.Instanced(),
			routine: world.RoutineBuilder().MultiThreaded(),
			callback: it =>
			{
				Column<Color> color = it.Field<Color>(1);

				foreach (int i in it)
					color[i].H += it.DeltaTime() % 1;
			}
		);

		world.Routine(
			name: "Render",
			filter: world.FilterBuilder()
				.With<RenderData>().Singleton()
				.With<Vector2, Position>()
				.With<Vector2, Scale>()
				.With<Color>()
				.Instanced(),
			routine: world.RoutineBuilder(),
			callback: it =>
			{
				ref RenderData renderData = ref it.Field<RenderData>(1)[0];
				ref MultiMesh multiMesh = ref renderData.MultiMesh;

				Column<Vector2> position = it.Field<Vector2>(2);
				Column<Vector2> scale = it.Field<Vector2>(3);
				Column<Color> color = it.Field<Color>(4);

				int count = it.Count();
				multiMesh.InstanceCount = count;
				multiMesh.VisibleInstanceCount = count;

				for (int i = 0; i < count; i++)
				{
					multiMesh.SetInstanceTransform2D(i, new Transform2D(0, scale[i], 0, position[i]));
					multiMesh.SetInstanceColor(i, color[i]);
				}
			}
		);
	}
}
