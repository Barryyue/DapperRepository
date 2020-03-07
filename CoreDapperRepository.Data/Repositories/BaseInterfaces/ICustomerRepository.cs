﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CoreDapperRepository.Core.Data;
using CoreDapperRepository.Core.Domain.Customers;

namespace CoreDapperRepository.Data.Repositories.BaseInterfaces
{
    public interface ICustomerRepository : IRepository<Customer>
    {
        Task<Customer> GetCustomerByIdAsync(int id);

        Task<Customer> GetCustomerByAsync(string name, string email);

        Task<IEnumerable<Customer>> GetAllCustomersAsync();

        Task<Tuple<int, IEnumerable<Customer>>> GetPagedCustomers(string username, string email, int pageIndex, int pageSize);

        Task<int> InsertCustomerAsync(Customer customer);

        Task<int> InsertCustomerListAsync(List<Customer> customers);

        Task<bool> UpdateCustomerAsync(Customer customer);

        Task<bool> DeleteCustomerAsync(Customer customer);
    }
}