using System;
using System.Reflection;
using System.Data;
using System.Data.Common;
using System.Collections.Generic;

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
			lock (_ConnectionConstructor) {
				IDbConnection conn = (IDbConnection)_ConnectionConstructor.Invoke(null);
				conn.ConnectionString = ConnectionString;
				conn.Open();
				return conn;
			}
		}

		public IDbCommand PrepareQuery(string Query, params object[] Parameters) {
			int ParameterI = 0;
			IDbConnection Connection = GetConnection();
			try {
				IDbCommand Command = Connection.CreateCommand();
				Command.CommandType = CommandType.Text;
				Command.CommandText = Query;
				Command.Parameters.Clear();
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
			} catch (Exception ex) {
				Connection.Close();
				throw ex;
			}
		}

		public int NonQuery(string QueryString, params object[] Parameters) {
			IDbCommand Command = PrepareQuery(QueryString, Parameters);
			try {
				return Command.ExecuteNonQuery();
			} finally {
				Command.Connection.Close();
			}
		}

		public object FetchField(string QueryString, params object[] Parameters) {
			IDbCommand Command = PrepareQuery(QueryString, Parameters);
			try {
				return Command.ExecuteScalar();
			} finally {
				Command.Connection.Close();
			}
		}
		public object[] FetchRow(string QueryString, params object[] Parameters) {
			IDbCommand Command = PrepareQuery(QueryString, Parameters);
			try {
				IDataReader Reader = Command.ExecuteReader();
				try {
					if (!Reader.Read()) return null;
					object[] Result = new object[Reader.FieldCount];
					Reader.GetValues(Result);
					return Result;
				} finally {
					Reader.Close();
				}
			} finally {
				Command.Connection.Close();
			}
		}
		public object[][] FetchRows(string QueryString, params object[] Parameters) {
			IDbCommand Command = PrepareQuery(QueryString, Parameters);
			try {
				IDataReader Reader = Command.ExecuteReader();
				try {
					List<object[]> Result = new List<object[]>();
					while (Reader.Read()) {
						object[] ResultArray = new object[Reader.FieldCount];
						Reader.GetValues(ResultArray);
						Result.Add(ResultArray);
					}
					return Result.ToArray();
				} finally {
					Reader.Close();
				}
			} finally {
				Command.Connection.Close();
			}
		}
		public void ForEachRow(Action<Object[]> f, string QueryString, params object[] Parameters) {
			IDbCommand Command = PrepareQuery(QueryString, Parameters);
			try {
				IDataReader Reader = Command.ExecuteReader();
				try {
					while (Reader.Read()) {
						object[] ResultArray = new object[Reader.FieldCount];
						Reader.GetValues(ResultArray);
						f(ResultArray);
					}
				} finally {
					Reader.Close();
				}
			} finally {
				Command.Connection.Close();
			}

		}

		/*public DBReader GetReader(string QueryString, params object[] Parameters) {
			return new DBReader(PrepareQuery(QueryString, Parameters));
		}*/
	}
}