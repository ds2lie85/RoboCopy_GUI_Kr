using RoboCopy_GUI_Kr.Core.Models;

namespace RoboCopy_GUI_Kr.Core.Jobs;

/// <summary>
/// 등록 순서를 유지하고 동시에 하나의 작업만 실행 상태로 전환하는 메모리 내 작업 큐입니다.
/// </summary>
public sealed class JobQueue
{
    private readonly object _gate = new();
    private readonly List<JobQueueEntry> _entries = [];

    public event EventHandler? Changed;

    public IReadOnlyList<JobQueueEntry> Items
    {
        get
        {
            lock (_gate)
            {
                return _entries.ToArray();
            }
        }
    }

    public int WaitingCount
    {
        get
        {
            lock (_gate)
            {
                return _entries.Count(entry => entry.State == JobQueueEntryState.Waiting);
            }
        }
    }

    public JobQueueEntry Enqueue(RobocopyJobOptions options, Guid? id = null)
    {
        ArgumentNullException.ThrowIfNull(options);

        var entry = new JobQueueEntry
        {
            Id = id ?? Guid.NewGuid(),
            Options = options,
        };

        lock (_gate)
        {
            if (_entries.Any(existing => existing.Id == entry.Id))
            {
                throw new InvalidOperationException("A queue entry with the same identifier already exists.");
            }

            _entries.Add(entry);
        }

        OnChanged();
        return entry;
    }

    public JobQueueEntry AddPaused(RobocopyJobOptions options, Guid id)
    {
        ArgumentNullException.ThrowIfNull(options);

        var entry = new JobQueueEntry
        {
            Id = id,
            Options = options,
            State = JobQueueEntryState.Paused,
        };

        lock (_gate)
        {
            if (_entries.Any(existing => existing.Id == id))
            {
                throw new InvalidOperationException("A queue entry with the same identifier already exists.");
            }

            _entries.Add(entry);
        }

        OnChanged();
        return entry;
    }

    public bool TryStartNext(out JobQueueEntry? entry)
    {
        lock (_gate)
        {
            if (_entries.Any(existing => existing.State == JobQueueEntryState.Running))
            {
                entry = null;
                return false;
            }

            var index = _entries.FindIndex(existing => existing.State == JobQueueEntryState.Waiting);
            if (index < 0)
            {
                entry = null;
                return false;
            }

            entry = _entries[index] with
            {
                State = JobQueueEntryState.Running,
                UpdatedAt = DateTimeOffset.Now,
            };
            _entries[index] = entry;
        }

        OnChanged();
        return true;
    }

    public bool Update(Guid id, RobocopyJobOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var changed = false;

        lock (_gate)
        {
            var index = _entries.FindIndex(entry => entry.Id == id);
            if (index < 0 || _entries[index].State == JobQueueEntryState.Running)
            {
                return false;
            }

            _entries[index] = _entries[index] with
            {
                Options = options,
                State = JobQueueEntryState.Waiting,
                UpdatedAt = DateTimeOffset.Now,
            };
            changed = true;
        }

        if (changed)
        {
            OnChanged();
        }

        return changed;
    }

    public bool Remove(Guid id)
    {
        var removed = false;

        lock (_gate)
        {
            var index = _entries.FindIndex(entry => entry.Id == id);
            if (index < 0 || _entries[index].State == JobQueueEntryState.Running)
            {
                return false;
            }

            _entries.RemoveAt(index);
            removed = true;
        }

        if (removed)
        {
            OnChanged();
        }

        return removed;
    }

    public bool SetState(Guid id, JobQueueEntryState state)
    {
        var changed = false;

        lock (_gate)
        {
            var index = _entries.FindIndex(entry => entry.Id == id);
            if (index < 0)
            {
                return false;
            }

            _entries[index] = _entries[index] with
            {
                State = state,
                UpdatedAt = DateTimeOffset.Now,
            };
            changed = true;
        }

        if (changed)
        {
            OnChanged();
        }

        return changed;
    }

    private void OnChanged()
    {
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
