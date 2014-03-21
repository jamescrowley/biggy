﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data;
using System.Data.Common;
using Npgsql;
using System.Configuration;

namespace Biggy.Postgres
{
  public class PGContext : BiggyRelationalContext
  {
    public PGContext(string connectionStringName) : base(connectionStringName) { }

    public override string DbDelimiterFormatString {
      get { return "\"{0}\""; }
    }

    public override DbConnection OpenConnection() {
      var result = new NpgsqlConnection(this.ConnectionString);
      result.Open();
      return result;
    }

    protected override void LoadDbColumnsList() {
      this.DbColumnsList = new List<DbColumnMapping>();
      var sql = ""
        + "SELECT c.TABLE_NAME, c.COLUMN_NAME, kcu.CONSTRAINT_NAME, c.DATA_TYPE, c.CHARACTER_MAXIMUM_LENGTH, tc.CONSTRAINT_TYPE, "
          + "CASE tc.CONSTRAINT_TYPE WHEN 'PRIMARY KEY' THEN CAST(1 AS BIt) ELSE CAST(0 AS Bit) END AS IsPrimaryKey, "
          + "(CASE ((SELECT CASE (LENGTH(pg_get_serial_sequence(quote_ident(c.TABLE_NAME), c.COLUMN_NAME)) > 0) WHEN true THEN 1 ELSE 0 END) + "
          + "(SELECT CASE (SELECT pgc.relkind FROM pg_class pgc WHERE pgc.relname = c.TABLE_NAME || '_' || c.COLUMN_NAME || '_' || 'seq') WHEN 'S' THEN 1 ELSE 0 END)) "
          + "WHEN 0 THEN false ELSE true END) AS IsAuto "
          + "FROM information_schema.columns c "
          + "LEFT OUTER JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE kcu "
        + "ON c.TABLE_SCHEMA = kcu.CONSTRAINT_SCHEMA AND c.TABLE_NAME = kcu.TABLE_NAME AND c.COLUMN_NAME = kcu.COLUMN_NAME "
        + "LEFT OUTER JOIN INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc "
        + "ON kcu.CONSTRAINT_SCHEMA = tc.CONSTRAINT_SCHEMA AND kcu.CONSTRAINT_NAME = tc.CONSTRAINT_NAME "
        + "WHERE c.TABLE_SCHEMA = 'public'";

      using (var conn = this.OpenConnection()) {
        using (var cmd = this.CreateCommand(sql, conn)) {
          var dr = cmd.ExecuteReader();
          while (dr.Read()) {
            var clm = dr["COLUMN_NAME"] as string;
            var newColumnMapping = new DbColumnMapping(this.DbDelimiterFormatString) {
              TableName = dr["TABLE_NAME"] as string,
              ColumnName = clm,
              PropertyName = clm,
              IsPrimaryKey = (bool)dr["IsPrimaryKey"],
              IsAutoIncementing = (bool)dr["IsAuto"]
            };
            this.DbColumnsList.Add(newColumnMapping);
          }
        }
      }
    }

    protected override void LoadDbTableNames() {
      this.DbTableNames = new List<string>();
      var sql = "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'public'";
      using (var conn = this.OpenConnection()) {
        using (var cmd = this.CreateCommand(sql, conn)) {
          var dr = cmd.ExecuteReader();
          while (dr.Read()) {
            this.DbTableNames.Add(dr.GetString(0));
          }
        }
      }
    }

    public override string GetInsertReturnValueSQL(string delimitedPkColumn) {
      return " RETURNING " + delimitedPkColumn + " as newId";
    }

    public override string GetSingleSelect(string delimitedTableName, string where) {
      return string.Format("SELECT * FROM {0} WHERE {1} LIMIT 1", delimitedTableName, where);
    }

    public override string BuildSelect(string where, string orderBy, int limit) {
      string sql = "SELECT {0} FROM {1} ";
      if (!string.IsNullOrEmpty(where)) {
        sql += where.Trim().StartsWith("where", StringComparison.OrdinalIgnoreCase) ? where : " WHERE " + where;
      }
      if (!String.IsNullOrEmpty(orderBy)) {
        sql += orderBy.Trim().StartsWith("order by", StringComparison.OrdinalIgnoreCase) ? orderBy : " ORDER BY " + orderBy;
      }

      if (limit > 0) {
        sql += " LIMIT " + limit;
      }
      return sql;
    }
  }
}
