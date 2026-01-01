using Delgato.Core;
using Delgato.Core.AgentTree;

namespace Delgato.Tests.Behaviors;

/// <summary>
/// TinyBDD behavior tests for Agent Tree functionality.
/// </summary>
public class AgentTreeBehaviors
{
    private AgentTree? _tree;
    private AgentNode? _rootNode;
    private AgentNode? _childNode;
    private SpawnResult? _spawnResult;

    #region Given/When/Then Helpers

    private void Given_An_Empty_Tree()
    {
        _tree = new AgentTree
        {
            CorrelationId = Guid.NewGuid().ToString(),
            MaxDepth = 5,
            MaxNodes = 10
        };
    }

    private void Given_A_Tree_With_Root_Node()
    {
        Given_An_Empty_Tree();
        _rootNode = CreateNode(0);
        _tree!.SetRoot(_rootNode);
    }

    private void Given_A_Child_Node()
    {
        _childNode = CreateNode(1);
    }

    private void When_Root_Is_Set()
    {
        _rootNode = CreateNode(0);
        _tree!.SetRoot(_rootNode);
    }

    private void When_Child_Is_Spawned()
    {
        _childNode = CreateNode(_rootNode!.Depth + 1);
        _spawnResult = _tree!.SpawnChild(_rootNode.NodeId, _childNode);
    }

    private void When_Tree_Is_Started()
    {
        _tree!.Start();
    }

    private void When_Tree_Is_Completed()
    {
        _tree!.Complete();
    }

    #endregion

    [Fact]
    public void Tree_Should_Accept_Root_Node()
    {
        // Given
        Given_An_Empty_Tree();

        // When
        When_Root_Is_Set();

        // Then
        Assert.NotNull(_tree!.Root);
        Assert.Equal(_rootNode, _tree.Root);
        Assert.Equal(1, _tree.NodeCount);
    }

    [Fact]
    public void Tree_Should_Spawn_Child_Nodes()
    {
        // Given
        Given_A_Tree_With_Root_Node();
        Given_A_Child_Node();

        // When
        When_Child_Is_Spawned();

        // Then
        Assert.True(_spawnResult!.IsSuccess);
        Assert.Equal(2, _tree!.NodeCount);
        Assert.Single(_rootNode!.Children);
    }

    [Fact]
    public void Tree_Should_Enforce_Max_Depth()
    {
        // Given
        _tree = new AgentTree
        {
            CorrelationId = Guid.NewGuid().ToString(),
            MaxDepth = 2,
            MaxNodes = 100
        };
        _rootNode = CreateNode(0);
        _tree.SetRoot(_rootNode);

        // When - spawn at depth 2 (exceeds max)
        var deepChild = CreateNode(2);
        var result = _tree.SpawnChild(_rootNode.NodeId, deepChild);

        // Then
        Assert.False(result.IsSuccess);
        Assert.Contains("depth", result.ErrorMessage!.ToLower());
    }

    [Fact]
    public void Tree_Should_Enforce_Max_Nodes()
    {
        // Given
        _tree = new AgentTree
        {
            CorrelationId = Guid.NewGuid().ToString(),
            MaxDepth = 10,
            MaxNodes = 2
        };
        _rootNode = CreateNode(0);
        _tree.SetRoot(_rootNode);

        // Add first child (should succeed)
        var child1 = CreateNode(1);
        _tree.SpawnChild(_rootNode.NodeId, child1);

        // When - try to add second child (should fail)
        var child2 = CreateNode(1);
        var result = _tree.SpawnChild(_rootNode.NodeId, child2);

        // Then
        Assert.False(result.IsSuccess);
        Assert.Contains("nodes", result.ErrorMessage!.ToLower());
    }

    [Fact]
    public void Tree_Should_Track_State_Transitions()
    {
        // Given
        Given_A_Tree_With_Root_Node();

        // When
        When_Tree_Is_Started();

        // Then
        Assert.Equal(AgentTreeState.Running, _tree!.State);
        Assert.NotNull(_tree.StartedAt);
    }

    [Fact]
    public void Tree_Should_Complete_Successfully()
    {
        // Given
        Given_A_Tree_With_Root_Node();
        When_Tree_Is_Started();

        // When
        When_Tree_Is_Completed();

        // Then
        Assert.Equal(AgentTreeState.Completed, _tree!.State);
        Assert.NotNull(_tree.CompletedAt);
    }

    [Fact]
    public void Tree_Should_Calculate_Summary()
    {
        // Given
        Given_A_Tree_With_Root_Node();
        _rootNode!.Metrics.TokensUsed = 100;
        _rootNode.Metrics.Cost = 0.01m;

        var child = CreateNode(1);
        child.Metrics.TokensUsed = 50;
        child.Metrics.Cost = 0.005m;
        _tree!.SpawnChild(_rootNode.NodeId, child);

        // When
        var summary = _tree.GetSummary();

        // Then
        Assert.Equal(2, summary.NodeCount);
        Assert.Equal(150, summary.TotalTokensUsed);
        Assert.Equal(0.015m, summary.TotalCost);
    }

    [Fact]
    public void Tree_Should_Support_BFS_Traversal()
    {
        // Given
        Given_A_Tree_With_Root_Node();
        var child1 = CreateNode(1);
        var child2 = CreateNode(1);
        _tree!.SpawnChild(_rootNode!.NodeId, child1);
        _tree.SpawnChild(_rootNode.NodeId, child2);

        // When
        var traversed = _tree.TraverseBfs().ToList();

        // Then
        Assert.Equal(3, traversed.Count);
        Assert.Equal(_rootNode, traversed[0]);
    }

    [Fact]
    public void Tree_Should_Support_DFS_Traversal()
    {
        // Given
        Given_A_Tree_With_Root_Node();
        var child1 = CreateNode(1);
        var child2 = CreateNode(1);
        _tree!.SpawnChild(_rootNode!.NodeId, child1);
        _tree.SpawnChild(_rootNode.NodeId, child2);

        // When
        var traversed = _tree.TraverseDfs().ToList();

        // Then
        Assert.Equal(3, traversed.Count);
        Assert.Equal(_rootNode, traversed[0]);
    }

    [Fact]
    public void Node_Should_Track_Path_From_Root()
    {
        // Given
        Given_A_Tree_With_Root_Node();
        var child = CreateNode(1);
        _tree!.SpawnChild(_rootNode!.NodeId, child);
        var grandchild = CreateNode(2);
        _tree.SpawnChild(child.NodeId, grandchild);

        // When
        var path = grandchild.GetPathFromRoot();

        // Then
        Assert.Equal(3, path.Count);
        Assert.Equal(_rootNode, path[0]);
        Assert.Equal(child, path[1]);
        Assert.Equal(grandchild, path[2]);
    }

    private AgentNode CreateNode(int depth)
    {
        return new AgentNode
        {
            NodeId = Guid.NewGuid(),
            AgentId = new AgentId($"agent-{Guid.NewGuid():N}"),
            Definition = new AgentDefinition
            {
                Id = new AgentId("test"),
                Name = "Test Agent"
            },
            Depth = depth
        };
    }
}
