using System;
using System.Collections.Generic;
using System.Linq;

namespace GambaWhere.State;

/// <summary>The character's concurrent hosting sessions, capped at MaxConcurrent.</summary>
public class HostSessions
{
    public const int MaxConcurrent = 5;

    private readonly List<SessionState> _sessions = new();
    private readonly object _gate = new();

    public IReadOnlyList<SessionState> Snapshot()
    {
        lock (_gate)
            return _sessions.ToArray();
    }

    public int Count
    {
        get
        {
            lock (_gate)
                return _sessions.Count;
        }
    }

    public bool AnyActive => Count > 0;

    public bool AtCapacity => Count >= MaxConcurrent;

    public SessionState? Find(string eventId)
    {
        lock (_gate)
            return _sessions.FirstOrDefault(s => s.EventId == eventId);
    }

    public void Add(SessionState session)
    {
        lock (_gate)
            _sessions.Add(session);
    }

    public bool Remove(string eventId)
    {
        lock (_gate)
        {
            var index = _sessions.FindIndex(s => s.EventId == eventId);
            if (index < 0)
                return false;

            _sessions.RemoveAt(index);
            return true;
        }
    }

    public void Clear()
    {
        lock (_gate)
            _sessions.Clear();
    }

    public SessionState? Primary()
    {
        lock (_gate)
            return _sessions.OrderBy(s => s.StartedAt ?? DateTime.MaxValue).FirstOrDefault();
    }

    public HashSet<string> CharacterNames()
    {
        lock (_gate)
            return _sessions.Select(s => s.CharacterName).ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
}
