using TextMeshDOTS.Authoring;
using UnityEditor;
using UnityEngine.UIElements;

namespace TextMeshDOTS
{
    [CustomEditor(typeof(FontCollectionAsset))]
    public class FontCollectionInspector : Editor
    {
        public VisualTreeAsset visualTreeAsset;
        public override VisualElement CreateInspectorGUI()
        {
            VisualElement myInspector = new VisualElement();

            var container = visualTreeAsset.Instantiate();
            var button = container.Q<Button>();
            button.clicked += OnProcessButtonClicked;
            myInspector.Add(container);

            return myInspector;
        }       
        void OnProcessButtonClicked()
        {
            var fontCollectionAsset = (FontCollectionAsset)target;
            fontCollectionAsset.ProcessFonts();
        }
    }
}