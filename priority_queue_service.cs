using System;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;

namespace YourNamespace.Services
{
    public class QueueItem
    {
        public long Id { get; set; }
        public int Priority { get; set; }
        public string Payload { get; set; }
        public DateTime CreatedDate { get; set; }
        public int RetryCount { get; set; }
    }

    public class QueueStats
    {
        public int PendingCount { get; set; }
        public int ProcessingCount { get; set; }
        public int CompletedCount { get; set; }
        public int FailedCount { get; set; }
        public int TotalCount { get; set; }
    }

    public class PriorityQueueService
    {
        private readonly string _connectionString;
        private readonly string _workerId;

        public PriorityQueueService(string connectionString, string workerId = null)
        {
            _connectionString = connectionString;
            _workerId = workerId ?? Environment.MachineName;
        }

        public async Task<long> EnqueueAsync(int priority, string payload)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                using (var cmd = new SqlCommand("sp_PriorityQueue_Enqueue", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@Priority", priority);
                    cmd.Parameters.AddWithValue("@Payload", payload);

                    var result = await cmd.ExecuteScalarAsync();
                    return Convert.ToInt64(result);
                }
            }
        }

        public async Task<QueueItem> DequeueAsync(int lockDurationSeconds = 300)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                using (var cmd = new SqlCommand("sp_PriorityQueue_Dequeue", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@WorkerId", _workerId);
                    cmd.Parameters.AddWithValue("@LockDurationSeconds", lockDurationSeconds);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            return new QueueItem
                            {
                                Id = reader.GetInt64(0),
                                Priority = reader.GetInt32(1),
                                Payload = reader.GetString(2),
                                CreatedDate = reader.GetDateTime(3),
                                RetryCount = reader.GetInt32(4)
                            };
                        }
                    }
                }
            }
            return null;
        }

        public async Task CompleteAsync(long id)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                using (var cmd = new SqlCommand("sp_PriorityQueue_Complete", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@Id", id);
                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }

        public async Task<int> FailAsync(long id, string errorMessage, int maxRetries = 3)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                using (var cmd = new SqlCommand("sp_PriorityQueue_Fail", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@Id", id);
                    cmd.Parameters.AddWithValue("@ErrorMessage", errorMessage);
                    cmd.Parameters.AddWithValue("@MaxRetries", maxRetries);

                    var result = await cmd.ExecuteScalarAsync();
                    return Convert.ToInt32(result);
                }
            }
        }

        public async Task<QueueStats> GetStatsAsync()
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                using (var cmd = new SqlCommand("sp_PriorityQueue_GetStats", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            return new QueueStats
                            {
                                PendingCount = reader.GetInt32(0),
                                ProcessingCount = reader.GetInt32(1),
                                CompletedCount = reader.GetInt32(2),
                                FailedCount = reader.GetInt32(3),
                                TotalCount = reader.GetInt32(4)
                            };
                        }
                    }
                }
            }
            return null;
        }

        public async Task<int> CleanupAsync(int retentionDays = 7)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                using (var cmd = new SqlCommand("sp_PriorityQueue_Cleanup", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@RetentionDays", retentionDays);

                    var result = await cmd.ExecuteScalarAsync();
                    return Convert.ToInt32(result);
                }
            }
        }
    }
}