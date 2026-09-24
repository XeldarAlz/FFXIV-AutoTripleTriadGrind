using FFXIVClientStructs.FFXIV.Component.GUI;
using System.Runtime.InteropServices;

namespace AutoTripleTriadGrind.Core.Triad.Addons;

// The Triple Triad windows expose no typed layout for their rule, name and texture nodes, so they are found by
// position in the node tree. Each lookup checks the child count it expects and returns null on any other layout.
internal static unsafe class NodeWalk
{
    private const int MaxSiblings = 64;
    private const int MaxDepth = 24;
    private const int ComponentNodeTypeStart = 1000;

    public static bool IsVisible(AtkResNode* node) => node is not null && node->IsVisible();

    public static int ChildCount(AtkResNode* node)
    {
        if (node is null || node->ChildNode is null)
        {
            return 0;
        }

        var count = 1;
        for (var sibling = node->ChildNode->PrevSiblingNode; sibling is not null && count < MaxSiblings; sibling = sibling->PrevSiblingNode)
        {
            count++;
        }

        return count;
    }

    // Children are walked from ChildNode through PrevSiblingNode, which is the order the reference layouts count in.
    public static AtkResNode* Child(AtkResNode* node, int index, int expectedCount)
    {
        if (ChildCount(node) != expectedCount || index >= expectedCount)
        {
            return null;
        }

        var child = node->ChildNode;
        for (var step = 0; step < index; step++)
        {
            child = child->PrevSiblingNode;
        }

        return child;
    }

    public static AtkResNode* ComponentChild(AtkResNode* maybeComponent, int index, int expectedCount)
    {
        if (maybeComponent is null || (int)maybeComponent->Type < ComponentNodeTypeStart)
        {
            return null;
        }

        return ComponentChild(((AtkComponentNode*)maybeComponent)->Component, index, expectedCount);
    }

    public static AtkResNode* ComponentChild(AtkComponentBase* component, int index, int expectedCount)
    {
        if (component is null || component->UldManager.NodeListCount != expectedCount || index >= expectedCount)
        {
            return null;
        }

        return component->UldManager.NodeList[index];
    }

    public static string? Text(AtkResNode* node)
    {
        if (node is null || node->Type != NodeType.Text)
        {
            return null;
        }

        return Marshal.PtrToStringUTF8((nint)((AtkTextNode*)node)->NodeText.StringPtr.Value);
    }

    public static string? TexturePath(AtkResNode* node)
    {
        if (node is null || node->Type != NodeType.Image)
        {
            return null;
        }

        var image = (AtkImageNode*)node;
        if (image->PartsList is null || image->PartId > image->PartsList->PartCount)
        {
            return null;
        }

        var asset = image->PartsList->Parts[image->PartId].UldAsset;
        if (asset is null || asset->AtkTexture.TextureType != TextureType.Resource || asset->AtkTexture.Resource is null)
        {
            return null;
        }

        var handle = asset->AtkTexture.Resource->TexFileResourceHandle;
        return handle is null ? null : handle->ResourceHandle.FileName.ToString();
    }

    public static bool AnyVisibleText(AtkResNode* node, Func<string, bool> matches, int depth = 0)
    {
        if (depth > MaxDepth || !IsVisible(node))
        {
            return false;
        }

        if (node->Type == NodeType.Text && Text(node) is { } text && matches(text))
        {
            return true;
        }

        if (node->ChildNode is null)
        {
            return false;
        }

        var visited = 0;
        for (var child = node->ChildNode; child is not null && visited < MaxSiblings; child = child->PrevSiblingNode)
        {
            visited++;
            if (AnyVisibleText(child, matches, depth + 1))
            {
                return true;
            }
        }

        return false;
    }
}
