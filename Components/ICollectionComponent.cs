using Unity.Jobs;
using UnityEngine;

namespace TextMeshDOTS
{
    /// <summary>
    /// A Pseduo-component that can be attached to entities.
    /// It can store NativeContainers and automatically tracks their dependencies.
    /// </summary>
    public interface ICollectionComponent
    {
        /// <summary>
        /// Attempt to Dispose the collection component. Note that user code could add not-fully-allocated collection components
        /// or a collection component may be default-initialized by the presence of an AssociatedComponentType.
        /// </summary>
        /// <param name="inputDeps"></param>
        /// <returns></returns>
        JobHandle TryDispose(JobHandle inputDeps);
    }
}
