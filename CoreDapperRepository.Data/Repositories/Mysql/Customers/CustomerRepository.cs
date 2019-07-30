﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Text;
using CoreDapperRepository.Core.Configuration;
using Dapper;
using CoreDapperRepository.Core.Data;
using CoreDapperRepository.Core.Domain.Customers;
using CoreDapperRepository.Data.Repositories.BaseInterfaces;

namespace CoreDapperRepository.Data.Repositories.Mysql.Customers
{
    public class CustomerRepository : MysqlRepositoryBase<Customer>, ICustomerRepository, IMysqlRepository
    {
        public CustomerRepository(IDbConnConfig connConfig) : base(connConfig)
        {
        }

        public Customer GetCustomerById(int id)
        {
            if (id == 0)
                return null;

            string sql = $"SELECT Id,Username,Email,Active,CreationTime FROM {TableName} WHERE Id = @id";
            return GetById(sql, new { id }, commandType: CommandType.Text);
        }

        public CustomerDtoModel GetCustomerBy(int id)
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendFormat("SELECT c.Id,c.Username,c.Email,c.Active,c.CreationTime,cr.Id,cr.Name,cr.SystemName FROM {0} c ", TableName);
            builder.Append("JOIN `Customer_CustomerRole_Mapping` crm ON c.Id = crm.CustomerId ");
            builder.Append("JOIN `CustomerRole` cr ON crm.CustomerRoleId = cr.Id WHERE c.Id = @id");

            string sql = builder.ToString();
            IDbSession session = DbSession;

            try
            {
                var customers = session.Connection.Query<CustomerDtoModel, CustomerRole, CustomerDtoModel>(sql, (c, cr) =>
                    {
                        c.CustomerRole = cr;
                        return c;
                    }, new { id }).FirstOrDefault();

                return customers;
            }
            catch (Exception)
            {
                return null;
            }
            finally
            {
                session.Dispose();
            }
        }

        public int GetCustomerCount(string username = "", string email = "")
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendFormat("SELECT COUNT({0}) FROM {1} WHERE 1=1 ", (string.IsNullOrEmpty(username) && string.IsNullOrEmpty(email)) ? "*" : "Username", TableName);
            IDbSession session = DbSession;

            try
            {
                int result;

                if (!string.IsNullOrEmpty(username) || !string.IsNullOrEmpty(email))
                {
                    DynamicParameters parameters = new DynamicParameters();

                    if (!string.IsNullOrEmpty(username))
                    {
                        builder.Append("AND Username LIKE CONCAT(@Username,'%') ");
                        parameters.Add("Username", username, DbType.String);
                    }
                    if (!string.IsNullOrEmpty(email))
                    {
                        builder.Append("AND Email LIKE CONCAT(@Email,'%')");
                        parameters.Add("Email", email, DbType.String);
                    }
                    builder.Append(";");

                    result = session.Connection.Query<int>(builder.ToString(), parameters, commandType: CommandType.Text).FirstOrDefault();
                }
                else
                {
                    builder.Append(";");
                    result = session.Connection.Query<int>(builder.ToString(), commandType: CommandType.Text).FirstOrDefault();
                }

                return result;
            }
            catch
            {
                return 0;
            }
            finally
            {
                session.Dispose();
            }
        }

        public int InsertList(out long time, List<Customer> customers, int roleId)
        {
            // 用于获取插入运行时间
            Stopwatch stopwatch = new Stopwatch();

            StringBuilder builder = new StringBuilder(50);
            builder.AppendFormat("INSERT INTO {0}( Username,Email,Active,CreationTime ) VALUES ( @Username,@Email,@Active,@CreationTime );", TableName);
            builder.AppendFormat("INSERT INTO `Customer_CustomerRole_Mapping`( CustomerId,CustomerRoleId ) VALUES ( LAST_INSERT_ID(),{0});", roleId);

            stopwatch.Start();

            int result = Execute(builder.ToString(), customers);

            stopwatch.Stop();

            time = stopwatch.ElapsedMilliseconds;

            return result;
        }

        public IEnumerable<CustomerDtoModel> GetAllCustomers()
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendFormat("SELECT c.Id,c.Username,c.Email,c.Active,c.CreationTime,cr.Id,cr.Name,cr.SystemName FROM {0} c ", TableName);
            builder.Append("JOIN `Customer_CustomerRole_Mapping` crm ON c.Id = crm.CustomerId ");
            builder.Append("JOIN `CustomerRole` cr ON crm.CustomerRoleId = cr.Id ORDER BY c.Id DESC");

            string sql = builder.ToString();
            IDbSession session = DbSession;

            try
            {
                session.BeginTrans();

                var customers = session.Connection.Query<CustomerDtoModel, CustomerRole, CustomerDtoModel>(sql, (c, cr) =>
                {
                    c.CustomerRole = cr;
                    return c;
                }, transaction: session.Transaction);
                session.Commit();

                return customers;
            }
            catch (Exception)
            {
                return null;
            }
            finally
            {
                session.Dispose();
            }
        }

        public IEnumerable<CustomerDtoModelForPage> GetPagedCustomers(int totalCount, string username = "", string email = "", int pageIndex = 0, int pageSize = int.MaxValue, bool useStoredProcedure = false)
        {
            IDbSession session = DbSession;

            int totalPage = totalCount <= pageSize ? 1 : totalCount > pageSize && totalCount < (pageSize * 2) ? 2 : totalCount / pageSize; // 总页数

            int midPage = totalPage / 2 + 1; //中间页数，大于该页数则采用倒排优化

            bool isLastPage = pageIndex == totalPage; // 是否最后一页，是最后一页则需要进行取模算出最后一页的记录数（可能小于PageSize）

            int descBound = (totalCount - pageIndex * pageSize); // 重新计算limit偏移量

            int lastPageSize = 0; // 计算最后一页的记录数

            if (isLastPage)
            {
                lastPageSize = totalCount % pageSize; // 取模得到最后一页的记录数
                descBound = descBound - lastPageSize; // 重新计算最后一页的偏移量
            }
            else
            {
                descBound = descBound - pageSize; // 正常重新计算除最后一页的偏移量
            }

            bool useDescOrder = pageIndex <= midPage; // 判断是否采取倒排优化

            DynamicParameters parameters = new DynamicParameters();

            parameters.Add("Username", username, DbType.String);
            parameters.Add("Email", email, DbType.String);
            parameters.Add("PageLowerBound", useDescOrder ? pageIndex * pageSize : descBound, DbType.Int32);
            parameters.Add("PageSize", isLastPage ? lastPageSize : pageSize, DbType.Int32);
            parameters.Add("UseDescOrder", useDescOrder ? 1 : 0, DbType.Int32);

            try
            {
                //session.BeginTrans();

                IEnumerable<CustomerDtoModelForPage> customers;

                if (useStoredProcedure)
                {
                    customers = session.Connection.Query<CustomerDtoModelForPage>("DRD_Customer_GetAllCustomers_Test", parameters, session.Transaction, commandType: CommandType.StoredProcedure);
                }
                else
                {
                    StringBuilder builder = new StringBuilder(50);

                    builder.AppendFormat("SELECT c.Id,c.Username,c.Email,c.Active,c.CreationTime,ccrm.CustomerRoleId FROM {0} c ", TableName);
                    builder.Append("INNER JOIN `Customer_CustomerRole_Mapping` ccrm ON c.Id = ccrm.CustomerId ");
                    builder.AppendFormat("INNER JOIN (SELECT Id FROM {0} WHERE 1 = 1 ", TableName);
                    if (!string.IsNullOrEmpty(username))
                    {
                        builder.Append("AND Username LIKE CONCAT(@Username,'%') ");
                    }
                    if (!string.IsNullOrEmpty(email))
                    {
                        builder.Append("AND Email LIKE CONCAT(@Email,'%') ");
                    }
                    builder.AppendFormat("{0} LIMIT @PageLowerBound, @PageSize) AS cu USING (Id) ", useDescOrder ? "ORDER BY Id DESC" : "ORDER BY Id");

                    builder.Append("ORDER BY c.Id DESC;");

                    customers = session.Connection.Query<CustomerDtoModelForPage>(builder.ToString(), parameters, session.Transaction, commandType: CommandType.Text);
                }

                //session.Commit();

                return customers;

            }
            catch
            {
                // log error

                return null;
            }
            finally
            {
                session.Dispose();
            }
        }

        public int InsertCustomer(Customer customer, int roleId)
        {
            StringBuilder builder = new StringBuilder(50);
            builder.AppendFormat("INSERT INTO {0}( Username,Email,Active,CreationTime ) VALUES ( @Username,@Email,@Active,@CreationTime );", TableName);
            builder.Append("INSERT INTO `Customer_CustomerRole_Mapping`( CustomerId,CustomerRoleId ) VALUES ( LAST_INSERT_ID(),@roleId );");

            return Execute(builder.ToString(), new
            {
                customer.Username,
                customer.Email,
                customer.Active,
                customer.CreationTime,
                roleId
            }, commandType: CommandType.Text);
        }

        /// <summary>
        /// 更新信息（事实上用户有可能具有多个角色，我这里为了演示方便就假设用户只有一个角色处理了）
        /// </summary>
        /// <param name="customer"></param>
        /// <param name="roleId">对应角色id</param>
        /// <returns></returns>
        public int UpdateCustomer(Customer customer, int roleId)
        {
            StringBuilder builder = new StringBuilder(50);
            builder.AppendFormat("UPDATE {0} SET Username = @Username,Email = @Email,Active = @Active WHERE Id = @Id;", TableName);
            builder.Append("UPDATE `Customer_CustomerRole_Mapping` SET CustomerRoleId = @CustomerRoleId WHERE CustomerId = @CustomerId;");

            return Execute(builder.ToString(), new
            {
                customer.Username,
                customer.Email,
                customer.Active,
                customer.Id,
                CustomerRoleId = roleId,
                CustomerId = customer.Id
            }, commandType: CommandType.Text);
        }
    }
}
