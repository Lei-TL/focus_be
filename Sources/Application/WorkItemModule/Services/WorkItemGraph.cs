namespace Application.WorkItemModule.Services;

// DFS thuần trên graph phụ thuộc, không đụng DB nên unit test trực tiếp được.
// Store gọi hàm này trong transaction để giữ nguyên tử check-then-insert.
public static class WorkItemGraph
{
    // Có đường depends-on từ start tới target không. Iterative + visited set,
    // không đệ quy nên graph sâu không tràn stack.
    public static bool HasPath(IEnumerable<(Guid From, Guid To)> edges, Guid start, Guid target)
    {
        if (start == target) return true;
        var adjacency = new Dictionary<Guid, List<Guid>>();
        foreach (var (from, to) in edges)
        {
            if (!adjacency.TryGetValue(from, out var list)) adjacency[from] = list = [];
            list.Add(to);
        }
        var visited = new HashSet<Guid> { start };
        var stack = new Stack<Guid>([start]);
        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if (!adjacency.TryGetValue(current, out var next)) continue;
            foreach (var node in next)
            {
                if (node == target) return true;
                if (visited.Add(node)) stack.Push(node);
            }
        }
        return false;
    }
}
