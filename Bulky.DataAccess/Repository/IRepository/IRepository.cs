using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace BulkyBook.DataAccess.Repository.IRepository
{
    public interface IRepository <T> where T : class
    {
        //T - Category

        //Get all categories
        IEnumerable<T> GetAll (Expression<Func<T, bool>>? filter=null, string? includeProperties = null);
        //Find function
        T Get (Expression<Func<T,bool>> filter, string? includeProperties = null, bool tracked = false);
        //Add Category
        void Add(T entity);
        //Remove Category
        void Remove (T entity);
        //Remove Multiple Categories
        void RemoveRange(IEnumerable<T> entities);


    }
}
