using System;
using System.Collections.Generic;
using System.Linq;

using SQLite;

using System.IO;
using System.Threading.Tasks;
using System.Reflection;
using System.Diagnostics;
using System.Linq.Expressions;

namespace program
{
    public class NomGroup
    {
        [PrimaryKey, AutoIncrement, NotNull]
        public int GroupId { get; set; }
        public string GroupName { get; set; }
    }

    public class Nom
    {
        [PrimaryKey, AutoIncrement, NotNull]
        public int NomId { get; set; }
        public string NomName { get; set; }
        public int NomGr { get; set; }
    }

    public class Market
    {
        [PrimaryKey, AutoIncrement, NotNull]
        public int MarketId { get; set; }
        public string MarketName { get; set; }
        public string MarketAddr { get; set; }
    }

    public class Purchases
    {
        [PrimaryKey, AutoIncrement, NotNull]
        public int PurchaseId { get; set; }
        public int PurchaseMarket { get; set; }
        public int PurchaseNom { get; set; }
        public int PurchaseNomСoast { get; set; }
    }

    public class Wallet
    {
        [PrimaryKey, AutoIncrement, NotNull]
        public int WalletId { get; set; }
        public string WalletName { get; set; }
        public int Balance { get; set; }
        public int BalanceDeltaDay { get; set; }
        public int DateTime { get; set; }
        public int IsActive { get; set; }
    }

    public enum TableName
    {
        nomgroup = 0,
        nom,
        market,
        purchases,
        wallets,
        notable,
        sqlitequery
    }

    public enum DisplayRole
    {
        EditableRole,
        DisplayRole,
    }

    public class SQLiteModel
    {
        private object Content;
        private object FilterContent;
        private object TableType;
        private Dictionary<int, string> HeaderData;

        private SQLiteAsyncConnection db;
        private bool bIsTableSet;
        private bool bIsOperational;
        private bool bIsFilter;

        private string TableName;
        private string FilterString;


        private delegate void ChangeHandler(int row, int col, dynamic value);
        private event ChangeHandler Change;


        public SQLiteModel(string dbName)
        {
            db = new SQLiteAsyncConnection(Path.Combine(Directory.GetCurrentDirectory(), dbName));
            Task.Run(() => { bIsOperational = false; PrepareDB(); });
            this.Change += new ChangeHandler(OnChange);
            Content = new List<object>();
            bIsTableSet = false;
            bIsOperational = true;
        }

        private async void PrepareDB()
        {
            try
            {
                string[] arr =
                    {
                        "create table if not exists NomGroup(" +
                            "GroupId integer primary key autoincrement not null, " +
                            "GroupName text not null)",

                        "create table if not exists Nom(" +
                            "NomId integer primary key autoincrement not null, " +
                            "NomName text not null, " +
                            "NomGr integer, " +
                            "foreign key(NomGr) references NomGroup(GroupId))",

                        "create table if not exists Market(" +
                            "MarketId integer primary key autoincrement not null, " +
                            "MarketName text not null, " +
                            "MarketAddr text)",

                        "create table if not exists Purchases(" +
                            "PurchaseId integer primary key autoincrement not null, " +
                            "PurchaseMarket integer not null, " +
                            "PurchaseNom integer not null, " +
                            "PurchaseNomСoast integer not null, " +
                            "foreign key(PurchaseMarket) references Market(MarketId), " +
                            "foreign key(PurchaseNom) references Nom(NomId))",

                        "create table if not exists Wallet(" +
                            "WalletId integer primary key autoincrement not null, " +
                            "WalletName string not null, " +
                            "Balance integer not null, " +
                            "BalanceDeltaDay integer not null, " +
                            "DateTime integer not null, " +
                            "IsActive integer not null)",

                    };
                for (var i = 0; i < arr.Count(); i++)
                {
                    await db.ExecuteScalarAsync<object>(arr[i]);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }

        public class OneResult
        {
            public string name { get; set; }
        }

        /// <summary>
        /// Set up table data to model, return false if table not exist
        /// </summary>
        /// <param name="tableName">table name from connected db</param>
        /// <returns></returns>
        public async Task<bool> SetTable(string tableName)
        {
            try
            {
                //get all tablenames from db 
                var r = await db.QueryAsync<OneResult>("select name from sqlite_master where type='table'");
                var result = r.ToList();
                foreach (OneResult item in result)
                {
                    if (item.name == tableName)
                    {
                        var className = "program." + item.name;
                        //get type of class, inplement 'tablename' table from db
                        //class should be implemented in program code separately
                        Type t = Type.GetType(className);

                        //make generic method SQLiteAsyncConnection.Table<TableName>
                        MethodInfo Table = typeof(SQLiteAsyncConnection).GetMethod("Table");
                        MethodInfo GenTable = Table.MakeGenericMethod(t);
                        var obj = GenTable.Invoke(db, null);

                        //get tabledata from db
                        //Use reflection to invoke metod of obj type(AsyncTableQuery.ToListAsync())
                        //get Result property, because ToListAsync() return type is Task<List<tableName>>
                        var tableList = obj.GetType().InvokeMember("ToListAsync", BindingFlags.InvokeMethod, null, obj, null);
                        var data = tableList.GetType().InvokeMember("Result", BindingFlags.GetProperty, null, tableList, null);

                        //get type of row in table (row type == tablename class)
                        Type dataType = data.GetType();
                        var row = dataType.InvokeMember("ToArray", BindingFlags.InvokeMethod, null, data, null);
                        var rowType = dataType.InvokeMember("ToArray", BindingFlags.InvokeMethod, null, data, null).GetType().InvokeMember("GetValue", BindingFlags.InvokeMethod, null, row, new object[] { 0 }).GetType();

                        var rowPropertiesInfo = rowType.GetProperties();
                        Dictionary<int, string> headerData = new Dictionary<int, string>();
                        for (var i = 0; i < rowPropertiesInfo.Count(); i++)
                        {
                            //headerData.Add(rowPropertiesInfo[i].Name);
                            headerData[i] = rowPropertiesInfo[i].Name;
                        }

                        //write global models properties, bIsTableSet & bIsOperational -> blocking parameters, check if no table set or if some operations running in current model...
                        TableType = rowType;
                        Content = data;
                        TableName = tableName;
                        HeaderData = headerData;
                        bIsTableSet = true;
                        bIsOperational = true;
                        return true;
                    }
                }
                Console.WriteLine("Failed here");
                return false;
            }
            catch (Exception e)
            {
                Console.WriteLine("Calling expression");
                Console.WriteLine(e.Message);
                return false;
            }
            
            

        }
        /// <summary>
        /// Clear model, free memory of model data and set model to nonoperational mode, untill table not been setting
        /// </summary>
        public void Clear()
        {
            Content = null;
            TableName = null;
            TableType = null;
            bIsTableSet = false;
            bIsOperational = false;

        }

        /// <summary>
        /// Filtering table data using SQL command behind WHERE and something else (ASC, DESC, ORDER BY) : SELECT * FROM somewhere WHERE [filterString here] ORDER BY something ASC,
        /// This method override data in model.
        /// </summary>
        /// <param name="filterString">filter string in SQL syntax, use  AND|OR to make difficult filter</param>
        public void Filter(string filterString)
        {
            string[] filters = filterString.Split(new string[]{" AND " , " OR "}, StringSplitOptions.RemoveEmptyEntries);
            foreach (var filter in filters)
            {
                string[] filterFields = filter.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                if (filterFields.Count() != 3)
                {
                    throw new NotSupportedException("Invalid Filter");
                } else
                {
                    var tableField = filterFields[0].ToString();
                    var constField = filterFields[2].ToString();
                    var exprField = filterFields[1].ToString();

                    //create lambda expression  x => x.Property == const property
                    ParameterExpression ExpParameter = Expression.Parameter((Type)TableType, "field");
                    Expression ExpProperty = Expression.Property(ExpParameter, tableField);
                    Expression ExpConst = Expression.Constant(constField);
                    MethodInfo EqualsMethod = typeof(String).GetMethod("Equals", new [] { typeof(string) });
                    Expression Equals = Expression.Call(ExpProperty, EqualsMethod, ExpConst);

                    //Get generic method Lambda<T>(Expression instance, ParameterExpression[] params)
                    MethodInfo Lambda = null;
                    var m = typeof(Expression).GetMethods();
                    foreach (var mn in m)
                    {
                        if (mn.Name == "Lambda")
                        {
                            var parameters = mn.GetParameters();
                            if (mn.IsGenericMethod)
                            {
                                foreach (var p in parameters)
                                {
                                    if (p.ParameterType == typeof(ParameterExpression[]) && parameters.Count() == 2)
                                    {
                                        Lambda = mn;
                                        break;
                                    }
                                }
                            }
                        }
                    }


                    //create generic method Lambda<Func<TableType, bool>>() and invoce it to get result expression like x => x.ParamName == "something"
                    var GenFunc = typeof(Func<,>).MakeGenericType(typeof(Wallet), typeof(bool));
                    var GenLambda = Lambda.MakeGenericMethod((Type)GenFunc);
                    var ResultExpression = GenLambda.Invoke(null, new object[] { Equals, new ParameterExpression[] { ExpParameter } });

                    //get table data from db using SQLiteAsyncConnection.Table<TableType>.Where(ResultExpression).ToListAsync();
                    //
                    MethodInfo GenTable = db.GetType().GetMethod("Table").MakeGenericMethod((Type)TableType);
                    var table = GenTable.Invoke(db, null);
                    object filterContent = table.GetType().InvokeMember("Where", BindingFlags.InvokeMethod, null, table, new object[] { ResultExpression });
                    var ToListAsync = filterContent.GetType().InvokeMember("ToListAsync", BindingFlags.InvokeMethod, null, filterContent, null);
                    var filterData = ToListAsync.GetType().InvokeMember("Result", BindingFlags.GetProperty, null, ToListAsync, null);

                    //Set up filterind db data to global property
                    Content = filterData;
                    FilterString = filterString;
                    bIsFilter = true;
                    bIsOperational = true;
                }
            }
        }
        /// <summary>
        /// Get count of TableType classes in current model
        /// </summary>
        /// <returns>(int)count of TableType classes in current model</returns>
        public int RowCount()
        {
            return Convert.ToInt16(Content.GetType().GetProperty("Count").GetValue(Content));
        }


        /// <summary>
        /// Get (T) class at specifyed position
        /// </summary>
        /// <typeparam name="T">Class type implemented database table</typeparam>
        /// <param name="id">position in model data</param>
        /// <returns>(T) class == row in db table</returns>
        public T Record<T>(int id)
        {
            if (bIsTableSet && bIsOperational)
            {
                var type = Content.GetType();
                var row = type.InvokeMember("ToArray", BindingFlags.InvokeMethod, null, Content, null);
                dynamic valueat = row.GetType().InvokeMember("GetValue", BindingFlags.InvokeMethod, null, row, new object[] { id });

                if (valueat.GetType() == typeof(T))
                {
                    return (T)valueat;
                } else
                {
                    throw new FormatException("Cannot convert object to " + typeof(T));
                }
            }
            else
            {
                throw new MethodAccessException("Method not ready: " + new StackTrace().GetFrame(0).GetMethod());
            }
        }

        /// <summary>
        /// Update data in model, 
        /// </summary>
        public async void Update()
        {
            if (bIsTableSet && bIsOperational)
            {
                if (bIsFilter)
                {
                    bIsOperational = false;
                    this.Filter(FilterString);
                } else
                {
                    bIsOperational = false;
                    await this.SetTable(TableName);
                }
            }
        }

        private async void OnChange(int row, int col, dynamic value)
        {
            var data = Content.GetType().InvokeMember("ToArray", BindingFlags.InvokeMethod, null, Content, null);
            var currentRow = data.GetType().InvokeMember("GetValue", BindingFlags.InvokeMethod, null, data, new object[] { row });

            var columns = TableType.GetType().GetProperties();
            var primary = columns[0].Name;
            var currentColumn = GetColumnName(col);
            foreach (var column in columns)
            {
                if (column.Name == currentColumn)
                {
                    column.SetValue(column, value);
                    await db.ExecuteAsync("update table ? set ? = ? where ? = ?", new object[] { TableName, currentColumn, value, primary, row });
                    currentRow.GetType().InvokeMember(currentColumn, BindingFlags.SetProperty, null, column, null);
                    data.GetType().InvokeMember("SetValue", BindingFlags.InvokeMethod, null, data, new object[] { currentRow, row });
                    Content = data.GetType().InvokeMember("ToList", BindingFlags.InvokeMethod, null, data, null);
                }
            }
            GetColumnName(col);
            List<string> list = new List<string>();
            list.ToArray().GetValue(row);
            list.ToArray().SetValue(list.ToArray(), row);
        }


        /// <summary>
        /// Get value of table class property by property name
        /// </summary>
        /// <param name="row">implement row of table in db</param>
        /// <param name="ColumnName">name of column in table </param>
        /// <returns></returns>
        public dynamic Data(int row, string ColumnName)
        {
            var data = Content.GetType().InvokeMember("ToArray", BindingFlags.InvokeMethod, null, Content, null);
            var currentRow = data.GetType().InvokeMember("GetValue", BindingFlags.InvokeMethod, null, data, new object[] { row });
            var rowProperties = currentRow.GetType().GetProperties();
            foreach (var property in rowProperties)
            {
                if (property.Name == ColumnName)
                {
                    dynamic cellData = property.GetValue(currentRow);
                    return Convert.ChangeType(cellData, property.PropertyType);
                }
            }
            return null;
        }
        
        private string GetColumnName(int col)
        {
            return HeaderData[col];
        }

        private int GetColumnNum(string columnName)
        {
            for (var i = 0; i < HeaderData.Count; i++)
            {
                if (HeaderData[i] == columnName)
                {
                    return i;
                }
            }
            throw new NullReferenceException("invalid column name");
        }
    }




    class Program
    {
        static async void Run()
        {
            SQLiteModel model = new SQLiteModel("mainDB.db");
            model.SetTable("Wallet").Wait();
            try
            {
                Console.WriteLine(model.RowCount());
                model.Filter("WalletName = MyWallet");
                Console.WriteLine(model.RowCount());
                Console.WriteLine(model.Data(0, "WalletName"));
                
            } catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }

        static void Main(string[] args)
        {
            Run();
            Console.ReadLine();
        }
    }
}