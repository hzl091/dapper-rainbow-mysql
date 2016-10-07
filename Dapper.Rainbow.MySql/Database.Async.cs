﻿//#if ASYNC
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dapper
{
	public abstract partial class Database<TDatabase> where TDatabase : Database<TDatabase>, new()
	{
		public partial class Table<T, TId>
		{
			/// <summary>
			/// Insert a row into the db asynchronously
			/// </summary>
			/// <param name="data">Either DynamicParameters or an anonymous type or concrete type</param>
			/// <returns></returns>
			public virtual async Task<long> InsertAsync (dynamic data)
			{
				var o = (object)data;
				List<string> paramNames = GetParamNames (o);
				paramNames.Remove ("Id");

				string cols = string.Join ("`,`", paramNames);
				string colsParams = string.Join (",", paramNames.Select (p => "@" + p));
				var sql = "INSERT INTO `" + TableName + "` (`" + cols + "`) VALUES (" + colsParams + "); SELECT LAST_INSERT_ID()";
				var id = (await database.QueryAsync (sql, o).ConfigureAwait (false)).Single () as IDictionary<string, object>;

				return Convert.ToInt64 (id.Values.Single ());
			}

			/// <summary>
			/// Update a record in the DB asynchronously
			/// </summary>
			/// <param name="id"></param>
			/// <param name="data"></param>
			/// <returns></returns>
			public Task<int> UpdateAsync (TId id, dynamic data)
			{
				return UpdateAsync (new { id }, data);
			}


			/// <summary>
			/// Update a record in the DB asynchronously
			/// </summary>
			/// <param name="where"></param>
			/// <param name="data"></param>
			/// <returns></returns>
			public Task<int> UpdateAsync (dynamic where, dynamic data)
			{
				List<string> paramNames = GetParamNames ((object)data);
				List<string> keys = GetParamNames ((object)where);

				var b = new StringBuilder ();
				b.Append ("UPDATE `").Append (TableName).Append ("` SET ");
				b.AppendLine (string.Join (",", paramNames.Select (p => "`" + p + "`= @" + p)));
				b.Append (" WHERE ").Append (string.Join (" AND ", keys.Select (p => "`" + p + "` = @" + p)));

				var parameters = new DynamicParameters (data);
				parameters.AddDynamicParams (where);
				return database.ExecuteAsync (b.ToString (), parameters);
			}


			/// <summary>
			/// Insert a row into the db or update when key is duplicated asynchronously
			/// only for autoincrement key
			/// </summary>
			/// <param name="id"></param>
			/// <param name="data">Either DynamicParameters or an anonymous type or concrete type</param>
			/// <returns></returns>
			public async Task<long> InsertOrUpdateAsync (TId id, dynamic data)
			{
				return await InsertOrUpdateAsync (new { id }, data);
			}

			/// <summary>
			/// Insert a row into the db or update when key is duplicated asynchronously
			/// for autoincrement key
			/// </summary>
			/// <param name="key"></param>
			/// <param name="data">Either DynamicParameters or an anonymous type or concrete type</param>
			/// <returns></returns>
			public async Task<long> InsertOrUpdateAsync (dynamic key, dynamic data)
			{
				List<string> paramNames = GetParamNames ((object)data);
				string k = GetParamNames ((object)key).Single ();

				string cols = string.Join ("`,`", paramNames);
				string cols_params = string.Join (",", paramNames.Select (p => "@" + p));
				string cols_update = string.Join (",", paramNames.Select (p => "`" + p + "` = @" + p));
				var b = new StringBuilder ();
				b.Append ("INSERT INTO `").Append (TableName).Append ("` (`").Append (cols).Append ("`,`").Append (k).Append ("`) VALUES (")
				 .Append (cols_params).Append (", @").Append (k)
				 .Append (") ON DUPLICATE KEY UPDATE ").Append ("`").Append (k).Append ("` = LAST_INSERT_ID(`").Append (k).Append ("`)")
				 .Append (", ").Append (cols_update).Append (";SELECT LAST_INSERT_ID()");
				var parameters = new DynamicParameters (data);
				parameters.AddDynamicParams (key);
				var id = (await database.QueryAsync (b.ToString (), parameters).ConfigureAwait (false)).Single () as IDictionary<string, object>;

				return Convert.ToInt64 (id.Values.Single ());
			}

			/// <summary>
			/// Delete a record for the DB asynchronously
			/// </summary>
			/// <param name="id"></param>
			/// <returns></returns>
			public async Task<bool> DeleteAsync (TId id)
			{
				return (await database.ExecuteAsync ("DELETE FROM `" + TableName + "` WHERE Id = @id", new { id }).ConfigureAwait (false)) > 0;
			}

			/// <summary>
			/// Grab a record with a particular Id from the DB asynchronously
			/// </summary>
			/// <param name="id"></param>
			/// <returns></returns>
			public async Task<T> GetAsync (TId id)
			{
				return (await database.QueryAsync<T> ("select * from `" + TableName + "` where Id = @id", new { id }).ConfigureAwait (false)).FirstOrDefault ();
			}

			public virtual async Task<T> FirstAsync (dynamic where = null)
			{
				if (where == null) return database.Query<T> ("SELECT * FROM `" + TableName + "` LIMIT 1").FirstOrDefault ();
				var paramNames = GetParamNames ((object)where);
				var w = string.Join (" AND ", paramNames.Select (p => "`" + p + "` = @" + p));
				return (await database.QueryAsync<T> ("SELECT * FROM `" + TableName + "` WHERE " + w + " LIMIT 1").ConfigureAwait (false)).FirstOrDefault ();
			}

			public Task<IEnumerable<T>> AllAsync (dynamic where = null)
			{
				var sql = "SELECT * FROM " + TableName;
				if (where == null) return database.QueryAsync<T> (sql);
				var paramNames = GetParamNames ((object)where);
				var w = string.Join (" AND ", paramNames.Select (p => "`" + p + "` = @" + p));
				return database.QueryAsync<T> (sql + " WHERE " + w, where);
			}
		}

		public Task<int> ExecuteAsync (string sql, dynamic param = null)
		{
			return connection.ExecuteAsync (sql, param as object, transaction, commandTimeout);
		}

		public Task<IEnumerable<T>> QueryAsync<T> (string sql, dynamic param = null)
		{
			return connection.QueryAsync<T> (sql, param as object, transaction, commandTimeout);
		}

		public Task<IEnumerable<TReturn>> QueryAsync<TFirst, TSecond, TReturn> (string sql, Func<TFirst, TSecond, TReturn> map, dynamic param = null, IDbTransaction transaction = null, bool buffered = true, string splitOn = "Id", int? commandTimeout = null)
		{
			return connection.QueryAsync (sql, map, param as object, transaction, buffered, splitOn);
		}

		public Task<IEnumerable<TReturn>> QueryAsync<TFirst, TSecond, TThird, TReturn> (string sql, Func<TFirst, TSecond, TThird, TReturn> map, dynamic param = null, IDbTransaction transaction = null, bool buffered = true, string splitOn = "Id", int? commandTimeout = null)
		{
			return connection.QueryAsync (sql, map, param as object, transaction, buffered, splitOn);
		}

		public Task<IEnumerable<TReturn>> QueryAsync<TFirst, TSecond, TThird, TFourth, TReturn> (string sql, Func<TFirst, TSecond, TThird, TFourth, TReturn> map, dynamic param = null, IDbTransaction transaction = null, bool buffered = true, string splitOn = "Id", int? commandTimeout = null)
		{
			return connection.QueryAsync (sql, map, param as object, transaction, buffered, splitOn);
		}

		public Task<IEnumerable<TReturn>> QueryAsync<TFirst, TSecond, TThird, TFourth, TFifth, TReturn> (string sql, Func<TFirst, TSecond, TThird, TFourth, TFifth, TReturn> map, dynamic param = null, IDbTransaction transaction = null, bool buffered = true, string splitOn = "Id", int? commandTimeout = null)
		{
			return connection.QueryAsync (sql, map, param as object, transaction, buffered, splitOn);
		}

		public Task<IEnumerable<dynamic>> QueryAsync (string sql, dynamic param = null)
		{
			return connection.QueryAsync (sql, param as object, transaction);
		}

		public Task<SqlMapper.GridReader> QueryMultipleAsync (string sql, dynamic param = null, IDbTransaction transaction = null, int? commandTimeout = null, CommandType? commandType = null)
		{
			return SqlMapper.QueryMultipleAsync (connection, sql, param, transaction, commandTimeout, commandType);
		}
	}
}
//#endif