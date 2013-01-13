using System;
using System.Collections.Generic;
using System.Data;
using System.Reflection;

namespace UCIS {
	public class Database {
		private delegate object ConstructorDelegate();
		private ConstructorInfo _ConnectionConstructor;

		public Database(Type DBConnectionType, string connectionString) {
			this.ConnectionString = connectionString;
			_ConnectionConstructor = DBConnectionType.GetConstructor(new Type[] { });
		}

		public string ConnectionString { get; set; }

		public virtual IDbConnection GetConnection() {
			IDbConnection conn = (IDbConnection)_ConnectionConstructor.Invoke(null);
			conn.ConnectionString = ConnectionString;
			conn.Open();
			return conn;
		}

		private IDbCommand PrepareQuery(IDbConnection Connection, string Query, params object[] Parameters) {
			IDbCommand Command = Connection.CreateCommand();
			Command.CommandType = CommandType.Text;
			Command.CommandText = Query;
			Command.Parameters.Clear();
			int ParameterI = 0;
			foreach (object Parameter in Parameters) {
				IDbDataParameter DBParameter = Command.CreateParameter();
				DBParameter.Direction = ParameterDirection.Input;
				DBParameter.ParameterName = "?" + ParameterI.ToString();
				DBParameter.Value = Parameter;
				Command.Parameters.Add(DBParameter);
				ParameterI++;
			}
			if (ParameterI > 0) Command.Prepare();
			return Command;
		}

		public IDbCommand PrepareQuery(string Query, params object[] Parameters) {
			IDbConnection Connection = GetConnection();
			try {
				return PrepareQuery(Connection, Query, Parameters);
			} catch (Exception) {
				Connection.Close();
				throw;
			}
		}

		public int NonQuery(string QueryString, params object[] Parameters) {
			using (IDbConnection connection = GetConnection()) {
				using (IDbCommand command = PrepareQuery(connection, QueryString, Parameters)) {
					return command.ExecuteNonQuery();
				}
			}
		}

		public object FetchField(string QueryString, params object[] Parameters) {
			using (IDbConnection connection = GetConnection()) {
				using (IDbCommand command = PrepareQuery(connection, QueryString, Parameters)) {
					return command.ExecuteScalar();
				}
			}
		}
		public object[] FetchRow(string QueryString, params object[] Parameters) {
			using (IDbConnection connection = GetConnection()) {
				using (IDbCommand command = PrepareQuery(connection, QueryString, Parameters)) {
					using (IDataReader Reader = command.ExecuteReader()) {
						if (!Reader.Read()) return null;
						object[] Result = new object[Reader.FieldCount];
						Reader.GetValues(Result);
						return Result;
					}
				}
			}
		}
		public object[][] FetchRows(string QueryString, params object[] Parameters) {
			using (IDbConnection connection = GetConnection()) {
				using (IDbCommand command = PrepareQuery(connection, QueryString, Parameters)) {
					using (IDataReader Reader = command.ExecuteReader()) {
						List<object[]> Result = new List<object[]>();
						while (Reader.Read()) {
							object[] ResultArray = new object[Reader.FieldCount];
							Reader.GetValues(ResultArray);
							Result.Add(ResultArray);
						}
						return Result.ToArray();
					}
				}
			}
		}
		public void ForEachRow(Action<Object[]> f, string QueryString, params object[] Parameters) {
			using (IDbConnection connection = GetConnection()) {
				using (IDbCommand command = PrepareQuery(connection, QueryString, Parameters)) {
					using (IDataReader Reader = command.ExecuteReader()) {
						while (Reader.Read()) {
							object[] ResultArray = new object[Reader.FieldCount];
							Reader.GetValues(ResultArray);
							f(ResultArray);
						}
					}
				}
			}
		}
		public void ForEachRow(Action<IDataRecord> callback, string query, params object[] parameters) {
			using (IDbConnection connection = GetConnection()) {
				using (IDbCommand command = PrepareQuery(connection, query, parameters)) {
					using (IDataReader Reader = command.ExecuteReader()) {
						while (Reader.Read()) callback(Reader);
					}
				}
			}
		}

		/*public DBReader GetReader(string QueryString, params object[] Parameters) {
			return new DBReader(PrepareQuery(QueryString, Parameters));
		}*/
	}
}