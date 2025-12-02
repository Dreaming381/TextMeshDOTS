using Unity.Collections;
using Unity.Jobs;

namespace TextMeshDOTS.Clipper2AoS
{
    public struct PolyTree
    {
        public NativeList<TreeNode> components;
        public NativeList<int> exteriorIDs;
        public bool GetNextComponent(int parentID, out int nextID)
        {
            nextID = components[parentID].childID;
            if (nextID != -1) //first try to go deep (to next child)
            {
                //Debug.Log("Go to child");
                return true;
            }
            nextID = components[parentID].rightID; //if that fails, try to go sideways (to next sibling)
            if (nextID != -1)
            {
                //Debug.Log("Go right");
                return true;
            }
            //if that fails, try to go to parent, check if right sibling exists, if not repeat until either parent is root (-1) or rightID exists
            do
            {
                //Debug.Log("Going up");
                nextID = components[parentID].rightID;
                parentID = components[parentID].parentID;
            } while (nextID == -1 && parentID != -1);
            if (nextID != -1) //return the next sibling that was found
            {
                //Debug.Log("Go Up-->right");
                return true;
            }
            if (parentID != -1) //last resort, return parent
            {
                //Debug.Log("Go Up-->parent");
                return true;
            }
            return false;
        }
        public void AddChildComponent(int parentID, int newChildID)
        {
            components.ElementAt(newChildID).parentID = parentID;
            if (components[parentID].childID == -1)
                components.ElementAt(parentID).childID = newChildID;
            else
            {
                int rightID = components[parentID].childID;
                int leftSiblingID;
                do
                {
                    leftSiblingID = rightID;
                    rightID = components[rightID].rightID;
                } while (rightID != -1);
                components.ElementAt(leftSiblingID).rightID = newChildID;
            }
        }

        public bool IsCreated;
        public PolyTree(int outerIDsize, Allocator allocator)
        {
            components = new NativeList<TreeNode>(outerIDsize, allocator);
            exteriorIDs = new NativeList<int>(outerIDsize, allocator);
            IsCreated = true;
        }
        public void Clear()
        {
            components.Clear();
            exteriorIDs.Clear();
        }
        public void Dispose()
        {
            if (components.IsCreated) components.Dispose();
            if (exteriorIDs.IsCreated) exteriorIDs.Dispose();
            IsCreated = false;
        }
        public void Dispose(JobHandle jobHandle)
        {
            if (components.IsCreated) components.Dispose(jobHandle);
            if (exteriorIDs.IsCreated) exteriorIDs.Dispose(jobHandle);
            IsCreated = false;
        }
    };
}