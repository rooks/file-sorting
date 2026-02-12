namespace FileSorting.Sorter;

/// <summary>
/// Tournament loser tree for efficient K-way merge.
/// Performs log2(k) comparisons per replacement vs ~2*log2(k) for a binary heap.
/// Fixed leaf-to-root path enables better branch prediction and cache locality.
/// </summary>
/// <remarks>
/// Tree layout for k leaves:
///   Internal nodes [0..k-1]: node[0] = overall winner index, nodes[1..k-1] = loser indices
///   Leaves [0..k-1]: current values from each input source
///
/// On ReplaceWinner: walk from the changed leaf to root. At each internal node,
/// compare the incoming value with the stored loser. The loser stays, the winner
/// propagates up. node[0] always holds the overall minimum's leaf index.
/// </remarks>
public sealed class LoserTree<T>
{
    private readonly int[] _nodes;    // internal nodes: leaf index of the loser (node[0] = winner)
    private readonly T[] _leaves;     // current value at each leaf
    private readonly bool[] _active;  // whether a leaf still produces values
    private readonly IComparer<T> _comparer;
    private readonly int _k;
    private int _activeCount;

    public int Count => _activeCount;

    /// <summary>Leaf index of the current overall winner.</summary>
    public int WinnerIndex => _nodes[0];

    /// <summary>Value of the current overall winner.</summary>
    public T WinnerValue => _leaves[_nodes[0]];

    public LoserTree(int capacity, IComparer<T> comparer)
    {
        _k = capacity;
        _comparer = comparer;
        _nodes = new int[_k];
        _leaves = new T[_k];
        _active = new bool[_k];
        _activeCount = 0;
    }

    /// <summary>
    /// Initialize leaf with a value. Call for each active source before Build().
    /// Leaves not initialized remain inactive and always lose.
    /// </summary>
    public void SetLeaf(int index, T value)
    {
        _leaves[index] = value;
        _active[index] = true;
        _activeCount++;
    }

    /// <summary>
    /// Build the tournament tree bottom-up after all leaves are set.
    /// O(k) comparisons — called once.
    /// </summary>
    public void Build()
    {
        if (_k <= 1)
        {
            if (_k == 1) _nodes[0] = 0;
            return;
        }

        // Bottom-up pairwise tournament.
        // winners[i] = leaf index that won the subtree rooted at tree position i.
        // Leaf positions: k..2k-1 (leaf i at position i+k).
        // Internal positions: 1..k-1. Node 0 stores the final winner.
        Span<int> winners = stackalloc int[2 * _k];

        for (var i = 0; i < _k; i++)
            winners[i + _k] = i;

        for (var i = _k - 1; i >= 1; i--)
        {
            var left = winners[2 * i];
            var right = winners[2 * i + 1];

            if (IsWinner(left, right))
            {
                _nodes[i] = right;
                winners[i] = left;
            }
            else
            {
                _nodes[i] = left;
                winners[i] = right;
            }
        }

        _nodes[0] = winners[1];
    }

    /// <summary>
    /// Replace the current winner's value and re-adjust the tree.
    /// This is the hot path — only log2(k) comparisons.
    /// </summary>
    public void ReplaceWinner(T newValue)
    {
        var idx = _nodes[0];
        _leaves[idx] = newValue;
        AdjustUp(idx);
    }

    /// <summary>
    /// Mark the current winner as exhausted and re-adjust.
    /// </summary>
    public void DeactivateWinner()
    {
        var idx = _nodes[0];
        _active[idx] = false;
        _leaves[idx] = default!;
        _activeCount--;
        AdjustUp(idx);
    }

    /// <summary>
    /// Walk from leaf to root. At each internal node, the loser stays and the winner
    /// propagates up. After completion, node[0] holds the new overall winner.
    /// </summary>
    private void AdjustUp(int leafIndex)
    {
        if (_k <= 1)
        {
            _nodes[0] = leafIndex;
            return;
        }

        var winner = leafIndex;
        var parent = (leafIndex + _k) >> 1;

        while (parent > 0)
        {
            var storedLoser = _nodes[parent];

            if (Loses(winner, storedLoser))
            {
                // winner is actually the loser; swap and carry stored up
                _nodes[parent] = winner;
                winner = storedLoser;
            }

            parent >>= 1;
        }

        _nodes[0] = winner;
    }

    /// <summary>
    /// Returns true if leaf a beats leaf b (a is smaller/equal, i.e., a should win).
    /// Inactive leaves never win.
    /// </summary>
    private bool IsWinner(int a, int b)
    {
        if (!_active[a]) return false;
        if (!_active[b]) return true;
        return _comparer.Compare(_leaves[a], _leaves[b]) <= 0;
    }

    /// <summary>
    /// Returns true if leaf a loses to leaf b (b is smaller, i.e., b wins).
    /// Inactive leaves always lose.
    /// </summary>
    private bool Loses(int a, int b)
    {
        if (!_active[a]) return true;
        if (!_active[b]) return false;
        return _comparer.Compare(_leaves[a], _leaves[b]) > 0;
    }
}
