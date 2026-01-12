using System;
using Unity.Entities;
using UnityEngine.InputSystem;

namespace TextMeshDOTS
{
    [DisableAutoCreation]
    public partial class InputSystem : SystemBase
    {
        InputActionAssetReference input = null!;
        InputStates inputStates;
        InputAction save;
        InputAction respawn;

        protected override void OnCreate()
        {
            RequireForUpdate<InputActionAssetReference>();
        }

        protected override void OnStartRunning()
        {
            input = SystemAPI.ManagedAPI.GetSingleton<InputActionAssetReference>();            
            input.Asset.Enable();
            var actions = input.Asset.FindActionMap("Player"); 

            respawn = actions["respawn"];
            respawn.canceled += OnRespawnPerformed;

            save = actions["save"];
            save.canceled += OnSavePerformed;
        }

        protected override void OnStopRunning()
        {
            respawn.canceled -= OnRespawnPerformed;
            save.canceled -= OnSavePerformed;            
            input.Asset.Disable();
        }

        protected override void OnUpdate()
        {
            if (inputStates.save == ButtonState.Canceled && !save.WasReleasedThisFrame())
                inputStates.save = ButtonState.Waiting;

            if (inputStates.respawn == ButtonState.Canceled && !respawn.WasReleasedThisFrame())
                inputStates.respawn = ButtonState.Waiting;

            SystemAPI.SetSingleton(inputStates);
        } 
        
        private void OnSavePerformed(InputAction.CallbackContext context)
        {
            inputStates.save = (ButtonState)context.phase;
        }
        private void OnRespawnPerformed(InputAction.CallbackContext context)
        {
            inputStates.respawn = (ButtonState)context.phase;
        }
    }
    [Serializable]
    public class InputActionAssetReference : IComponentData
    {
        public InputActionAsset Asset = null!;
        //public InputActionReference cursorPosition = null!;
    }

    public struct InputStates : IComponentData
    {
        public ButtonState save;
        public ButtonState respawn;
    }
    public enum ButtonState : byte
    {
        Disabled,
        Waiting,
        Started,
        Performed,
        Canceled,
    }    
}
