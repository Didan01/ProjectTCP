using System.Data;
using Dapper;

namespace Server.Database;

public class TimeOnlyHandler : SqlMapper.TypeHandler<TimeOnly> {
    public override TimeOnly Parse(object value) {
        if (value is TimeOnly t) return t;
        if (value is TimeSpan ts) return TimeOnly.FromTimeSpan(ts);
        return TimeOnly.Parse(value.ToString());
    }

    public override void SetValue(IDbDataParameter parameter, TimeOnly value) {
        parameter.DbType = DbType.Time;
        parameter.Value = value.ToTimeSpan();
    }
}