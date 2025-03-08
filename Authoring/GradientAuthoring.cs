using Unity.Entities;
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.TextCore.Text;

namespace TextMeshDOTS.Authoring
{ 
    [DisallowMultipleComponent]
    [AddComponentMenu("TextMeshDOTS/Text Color Gradient")]
    public class TextGradientAuthoring : MonoBehaviour
    {
        [Tooltip("For horizontal gradients, specify at least top-(left & right). For vertical gradients (top & bottom)-left. Otherwise specify all corner")]
        public List<TextMeshDOTSColorGradient> gradients;        
    }

    class TextGradientBaker : Baker<TextGradientAuthoring>
    {
        public override void Bake(TextGradientAuthoring authoring)
        {
            if (authoring.gradients == null)
                return;

            if (authoring.gradients.Count > 12)
            {
                Debug.Log("TextMeshDOTS supports currently only 12 gradients");
                return;
            }
            var entity = GetEntity(TransformUsageFlags.None);
            var calliByte = AddBuffer<TextColorGradient>(entity);
            for (int i = 0, ii = authoring.gradients.Count; i < ii; i++)
            {
                var gradient = authoring.gradients[i];
                TextColorGradient textColorGradient;
                if (gradient.colorMode==ColorGradientMode.HorizontalGradient)
                {
                    textColorGradient = new TextColorGradient
                    {
                        nameHash = TextHelper.GetValueHash(gradient.name),
                        topLeft = gradient.topLeft,
                        topRight = gradient.topRight,
                        bottomLeft = gradient.topLeft,
                        bottomRight = gradient.topRight,
                    };
                } 
                else if (gradient.colorMode == ColorGradientMode.VerticalGradient)
                {
                    textColorGradient = new TextColorGradient
                    {
                        nameHash = TextHelper.GetValueHash(gradient.name),
                        topLeft = gradient.topLeft,
                        topRight = gradient.topLeft,
                        bottomLeft = gradient.bottomLeft,
                        bottomRight = gradient.bottomLeft,
                    };
                }
                else
                {
                    textColorGradient = new TextColorGradient
                    {
                        nameHash = TextHelper.GetValueHash(gradient.name),
                        topLeft = gradient.topLeft,
                        topRight = gradient.topRight,
                        bottomLeft = gradient.bottomLeft,
                        bottomRight = gradient.bottomRight,
                    };
                }
                calliByte.Add(textColorGradient);
            }                
        }        
    }    
}