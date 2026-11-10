using System.Collections;
using System.Data.Common;

namespace Jobby.Tests.Postgres.Helpers;

/// <summary>
/// Minimal in-memory DbDataReader over a single row of named columns.
/// </summary>
internal class FakeDbDataReader : DbDataReader
{
    private readonly Dictionary<string, int> _ordinalByName;
    private readonly object?[] _values;
    private bool _read;

    /// <param name="columns">column name -> value; null value means DBNull (NULL in db)</param>
    /// <param name="hasRows">value of the HasRows property; when false, Read() also returns false</param>
    public FakeDbDataReader(IReadOnlyDictionary<string, object?> columns, bool hasRows = true)
    {
        _ordinalByName = columns
            .Select((kv, i) => (kv.Key, i))
            .ToDictionary(x => x.Key, x => x.i);
        _values = columns.Values.ToArray();
        HasRows = hasRows;
        _returnsRow = hasRows;
    }

    private readonly bool _returnsRow;

    public override bool HasRows { get; }
    public override int FieldCount => _values.Length;
    public override bool IsClosed => false;
    public override int Depth => 0;
    public override int RecordsAffected => 0;
    public override object this[int ordinal] => _values[ordinal] ?? DBNull.Value;
    public override object this[string name] => _values[GetOrdinal(name)] ?? DBNull.Value;

    public override bool Read()
    {
        if (_read || !_returnsRow)
        {
            return false;
        }
        _read = true;
        return true;
    }

    public override bool IsDBNull(int ordinal) => _values[ordinal] is null or DBNull;

    public override string GetString(int ordinal) => (string)_values[ordinal]!;
    public override int GetInt32(int ordinal) => (int)_values[ordinal]!;
    public override Guid GetGuid(int ordinal) => (Guid)_values[ordinal]!;
    public override bool GetBoolean(int ordinal) => (bool)_values[ordinal]!;
    public override DateTime GetDateTime(int ordinal) => (DateTime)_values[ordinal]!;

    public override int GetOrdinal(string name)
    {
        return _ordinalByName.TryGetValue(name, out var ordinal)
            ? ordinal
            : throw new ArgumentException($"Column '{name}' does not exist");
    }

    public override string GetName(int ordinal) => _ordinalByName.First(kv => kv.Value == ordinal).Key;
    public override object GetValue(int ordinal) => _values[ordinal] ?? DBNull.Value;
    public override Type GetFieldType(int ordinal) => _values[ordinal]?.GetType() ?? typeof(object);
    public override string GetDataTypeName(int ordinal) => GetFieldType(ordinal).Name;
    public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length) => throw new NotSupportedException();
    public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length) => throw new NotSupportedException();
    public override IEnumerator GetEnumerator() => _values.GetEnumerator();
    public override int GetValues(object[] values)
    {
        for (var i = 0; i < values.Length && i < _values.Length; i++)
        {
            values[i] = _values[i] ?? DBNull.Value;
        }
        return Math.Min(values.Length, _values.Length);
    }
    public override bool NextResult() => false;
    public override Task<bool> ReadAsync(CancellationToken cancellationToken) => Task.FromResult(Read());

    // typed getters that are not used by the code under test
    public override byte GetByte(int ordinal) => throw new NotSupportedException();
    public override char GetChar(int ordinal) => throw new NotSupportedException();
    public override decimal GetDecimal(int ordinal) => throw new NotSupportedException();
    public override double GetDouble(int ordinal) => throw new NotSupportedException();
    public override float GetFloat(int ordinal) => throw new NotSupportedException();
    public override short GetInt16(int ordinal) => throw new NotSupportedException();
    public override long GetInt64(int ordinal) => throw new NotSupportedException();
    public override Stream GetStream(int ordinal) => throw new NotSupportedException();
    public override TextReader GetTextReader(int ordinal) => throw new NotSupportedException();
}
