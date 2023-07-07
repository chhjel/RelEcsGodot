using Godot;
using System.Linq;

namespace RelEcs
{
    public class Root
    {
        public Node? Node;
    }

    public interface ISpawnable
    {
        void Spawn(EntityBuilder entityBuilder);
    }

    public partial class Marshallable<T> : RefCounted where T : class
    {
        public T Value;
        public Marshallable(T value) => Value = value;
    }

    public interface IRemovedOnDespawn { }

    public static class GodotExtensions
    {
        public static EntityBuilder Spawn(this World world, Node root)
        {
            return world.Spawn().Attach(root);
        }
        
        public static void DespawnAndFree(this World world, Entity entity)
        {
            // Handle IRemovedOnDespawn
            foreach (var comp in world.GetComponents(entity))
            {
                var type = comp.Item2?.GetType();
                if (type?.IsAssignableTo(typeof(IRemovedOnDespawn)) != true) continue;

                world.RemoveComponent(entity, type);
            }

            if (world.TryGetComponent<Root>(entity, out var root)) root!.Node?.QueueFree();
            world.Despawn(entity);
        }

        public static void AttachNode(this World world, Entity entity, Node root)
        {
            world.AddComponent(entity, new Root { Node = root });
            root.SetMeta("Entity", new Marshallable<Entity>(entity));

            var nodes = root.GetChildren().Cast<Node>()
                .Where(x => !x.Name.ToString().StartsWith("_"))
                .Prepend(root).ToList();

            foreach (var node in nodes)
            {
                var addMethod = typeof(GodotExtensions).GetMethod(nameof(AddNodeComponent));
                var addChildMethod = addMethod?.MakeGenericMethod(node.GetType());
                addChildMethod?.Invoke(null, new object[] { world, entity, node });
            }

            if (root is ISpawnable spawnable) spawnable.Spawn(new EntityBuilder(world, entity));
        }

        public static void AddNodeComponent<T>(World world, Entity entity, T node) where T : Node, new()
        {
            world.AddComponent(entity, node);
        }
        
        public static EntityBuilder Attach(this EntityBuilder entityBuilder, Node node)
        {
            entityBuilder.World.AttachNode(entityBuilder.Id(), node);
            return entityBuilder;
        }
    }
}