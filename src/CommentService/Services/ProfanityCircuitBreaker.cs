
namespace CommentService.Services;

public class CircuitOpenException : Exception
{
    public CircuitOpenException()
        : base("ProfanityService is temporarily unavailable.")
    {
    }
}

public class ProfanityCircuitBreaker
{
    private readonly object _lock = new();

    private int _failureCount = 0;
    private bool _isOpen = false;
    private bool _isHalfOpen = false;
    private bool _halfOpenRequestInProgress = false;
    private DateTime _openUntil;

    private const int FailureThreshold = 3; // Number of consecutive failures before opening the circuit
    private static readonly TimeSpan BreakDuration = TimeSpan.FromSeconds(15); // Duration to keep the circuit open before allowing a test request

    public async Task<T> ExecuteAsync<T>(Func<Task<T>> operation)
    {
        lock (_lock)
        {
            if (_isOpen)
            {
                if (DateTime.UtcNow < _openUntil)
                    throw new CircuitOpenException();

                _isOpen = false;
                _isHalfOpen = true; // Allow a test request
            }

            if (_isHalfOpen)
            {
                if (_halfOpenRequestInProgress)
                    throw new CircuitOpenException();

                _halfOpenRequestInProgress = true; // Allow only one test request in half-open state
            }
        }

        try
        {
            var result = await operation();

            lock (_lock)
            {
                _failureCount = 0;
                _isOpen = false;
                _isHalfOpen = false;
                _halfOpenRequestInProgress = false;
            }

            return result;
        }
        catch
        {
            lock (_lock)
            {
                _failureCount++;

                _halfOpenRequestInProgress = false;

                if (_isHalfOpen || _failureCount >= FailureThreshold)
                {
                    _isOpen = true;
                    _isHalfOpen = false;
                    _openUntil = DateTime.UtcNow + BreakDuration;
                }
            }

            throw;
        }
    }
}
