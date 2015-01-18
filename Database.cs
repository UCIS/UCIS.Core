using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;

namespace UCIS {
	public class Database {
		public delegate IDbConnection ConnectionConstructorDelegate();

		private ConnectionConstructorDelegate ConnectionConstructor;
		public string ConnectionString { get; private set; }

		public Database(Type connectionType, String connectionString) {
			this.ConnectionString = connectionString;
			this.ConnectionConstructor = delegate() { return (IDbConnection)Activator.CreateInstance(connectionType); };
		}
		public Database(DbProviderFactory factory, String connectionString) {
			this.ConnectionString = connectionString;
			this.ConnectionConstructor = factory.CreateConnection;
		}
		public Database(ConnectionConstructorDelegate constructor, String connectionString) {
			this.ConnectionString = connectionString;
			this.ConnectionConstructor = constructor;
		}

		public virtual IDbConnection GetConnection() {
			IDbConnection conn = ConnectionConstructor();
			conn.ConnectionString = ConnectionString;
			conn.Open();
			return conn;
		}

		private static IDbCommand PrepareQuery(IDbConnection connection, String query, params Object[] parameters) {
			IDbCommand command = connection.CreateCommand();
			try {
				command.CommandType = CommandType.Text;
				command.CommandText = query;
				command.Parameters.Clear();
				int index = 0;
				foreach (Object parameter in parameters) {
					IDbDataParameter dbparameter = command.CreateParameter();
					dbparameter.Direction = ParameterDirection.Input;
					dbparameter.ParameterName = "?" + index.ToString();
					dbparameter.Value = parameter;
					command.Parameters.Add(dbparameter);
					index++;
				}
				if (index > 0) command.Prepare();
			} catch {
				command.Dispose();
				throw;
			}
			return command;
		}

		public IDbCommand PrepareQuery(String query, params Object[] parameters) {
			IDbConnection connection = GetConnection();
			try {
				return PrepareQuery(connection, query, parameters);
			} catch {
				connection.Close();
				throw;
			}
		}

		public int NonQuery(String query, params Object[] parameters) {
			using (IDbConnection connection = GetConnection()) {
				using (IDbCommand command = PrepareQuery(connection, query, parameters)) {
					return command.ExecuteNonQuery();
				}
			}
		}

		public Object FetchField(String query, params Object[] parameters) {
			using (IDbConnection connection = GetConnection()) {
				using (IDbCommand command = PrepareQuery(connection, query, parameters)) {
					return command.ExecuteScalar();
				}
			}
		}
		public Object[] FetchRow(String query, params Object[] parameters) {
			using (IDbConnection connection = GetConnection()) {
				using (IDbCommand command = PrepareQuery(connection, query, parameters)) {
					using (IDataReader reader = command.ExecuteReader()) {
						if (!reader.Read()) return null;
						Object[] result = new Object[reader.FieldCount];
						reader.GetValues(result);
						return result;
					}
				}
			}
		}
		public Object[][] FetchRows(String query, params Object[] parameters) {
			using (IDbConnection connection = GetConnection()) {
				using (IDbCommand command = PrepareQuery(connection, query, parameters)) {
					using (IDataReader reader = command.ExecuteReader()) {
						List<Object[]> result = new List<Object[]>();
						while (reader.Read()) {
							Object[] resultarray = new Object[reader.FieldCount];
							reader.GetValues(resultarray);
							result.Add(resultarray);
						}
						return result.ToArray();
					}
				}
			}
		}
		public void ForEachRow(Action<Object[]> f, String query, params Object[] parameters) {
			using (IDbConnection connection = GetConnection()) {
				using (IDbCommand command = PrepareQuery(connection, query, parameters)) {
					using (IDataReader reader = command.ExecuteReader()) {
						while (reader.Read()) {
							Object[] resultarray = new Object[reader.FieldCount];
							reader.GetValues(resultarray);
							f(resultarray);
						}
					}
				}
			}
		}
		public void ForEachRow(Action<IDataRecord> callback, String query, params Object[] parameters) {
			using (IDbConnection connection = GetConnection()) {
				using (IDbCommand command = PrepareQuery(connection, query, parameters)) {
					using (IDataReader reader = command.ExecuteReader()) {
						while (reader.Read()) callback(reader);
					}
				}
			}
		}

		public IDataReader ExecuteReader(String query, params Object[] parameters) {
			IDbConnection connection = GetConnection();
			try {
				using (IDbCommand command = PrepareQuery(connection, query, parameters)) {
					return command.ExecuteReader(CommandBehavior.CloseConnection);
				}
			} catch {
				connection.Dispose();
				throw;
			}
		}

		public IEnumerable<IDataRecord> EnumerateRows(String query, params Object[] parameters) {
			IDbConnection connection = GetConnection();
			try {
				return new DataEnumerator(PrepareQuery(connection, query, parameters));
			} catch {
				connection.Dispose();
				throw;
			}
		}

		class DataEnumerator : IEnumerable<IDataRecord>, IEnumerator<IDataRecord> {
			IDbCommand command = null;
			IDataReader reader = null;
			public DataEnumerator(IDbCommand command) {
				this.command = command;
				try {
					this.reader = command.ExecuteReader();
				} catch {
					Dispose();
					throw;
				}
			}
			IEnumerator<IDataRecord> IEnumerable<IDataRecord>.GetEnumerator() {
				return this;
			}
			System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() {
				return this;
			}
			public IDataRecord Current {
				get { return reader; }
			}
			object System.Collections.IEnumerator.Current {
				get { return reader; }
			}
			public bool MoveNext() {
				return reader.Read();
			}
			public void Reset() {
				throw new NotSupportedException();
			}
			public Object[] CurrentRow {
				get {
					object[] array = new object[reader.FieldCount];
					reader.GetValues(array);
					return array;
				}
			}
			public void Dispose() {
				if (reader != null) reader.Dispose();
				if (command != null) {
					IDbConnection connection = command.Connection;
					command.Dispose();
					connection.Dispose();
				}
			}
		}
	}
}
