
using BulkyBook.DataAccess.Data;
using BulkyBook.DataAccess.Repository.IRepository;
using BulkyBook.Models;
using BulkyBook.Models.ViewModels;
using BulkyBook.Utility;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace BulkyBookWeb.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = SD.Role_Admin)]
    public class CompanyController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;
        public CompanyController(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public IActionResult Index()
        {
            List<Company> objCompanyList = _unitOfWork.Company.GetAll().ToList();
            return View(objCompanyList);
        }

        //combining create and update with one page
        
        public IActionResult Upsert(int? id)
        {

            //Get Category List using projection
            //IEnumerable<SelectListItem> CategoryList = _unitOfWork.Category
            //    .GetAll().Select(u => new SelectListItem
            //    {
            //        Text = u.Name,
            //        Value = u.Id.ToString()
            //    });
            //Using View Bag            
            //ViewBag.CategoryList = CategoryList;    

            //Using View Data
            //ViewData["CategoryList"] = CategoryList;

            //Using Company View Model
            

            if (id == null || id == 0)
            {
                //create
                return View(new Company());
            }
            else 
            {
                //update
                Company companyObj= _unitOfWork.Company.Get(u=>u.Id==id);
                return View(companyObj);
            }
            
        }

        [HttpPost]
        public IActionResult Upsert(Company companyObj)
        {
            
            if (ModelState.IsValid)
            {
                if(companyObj.Id == 0)
                {
                    _unitOfWork.Company.Add(companyObj);
                }
                else
                {
                    _unitOfWork.Company.Update(companyObj);
                }
                
                _unitOfWork.Save();
                TempData["success"] = "Company created successfuly";
                return RedirectToAction("Index");
            }
            else
            {
                return View(companyObj);
            }
        }

        public IActionResult Edit(int? id)
        {
            if (id == null || id == 0)
            {
                return NotFound();
            }
            //Multiple ways to retreive data from database
            Company? companyFromDb = _unitOfWork.Company.Get(u => u.Id == id);
            //Company? companyFromDb1 = _db.Categories.FirstOrDefault(x=>x.Id == id);
            //Company? companyFromDb2= _db.Categories.Where(u=>u.Id == id).FirstOrDefault();
            if (companyFromDb == null)
            {
                return NotFound();
            }
            return View(companyFromDb);
        }

        [HttpPost]
        public IActionResult Edit(Company obj)
        {

            if (ModelState.IsValid)
            {
                _unitOfWork.Company.Update(obj);
                _unitOfWork.Save();
                TempData["success"] = "Company updated successfuly";
                return RedirectToAction("Index");
            }
            return View();
        }

        //public IActionResult Delete(int? id)
        //{
        //    if (id == null || id == 0)
        //    {
        //        return NotFound();
        //    }
        //    Company? companyFromDb = _unitOfWork.Company.Get(u => u.Id == id);
        //    if (companyFromDb == null)
        //    {
        //        return NotFound();
        //    }
        //    return View(companyFromDb);
        //}

        //[HttpPost, ActionName("delete")]
        //public IActionResult DeletePOST(int? id)
        //{

        //    Company? obj = _unitOfWork.Company.Get(u => u.Id == id);
        //    if (obj == null)
        //    {
        //        return NotFound();
        //    }
        //    _unitOfWork.Company.Remove(obj);
        //    _unitOfWork.Save();
        //    TempData["success"] = "Company deleted successfuly";
        //    return RedirectToAction("Index");

        //}

        #region API CALLS
        [HttpGet]
        public IActionResult GetAll()
        {
            List<Company> objCompanyList = _unitOfWork.Company.GetAll().ToList();
            return Json(new { data = objCompanyList });
        }

        [HttpDelete]
        public IActionResult Delete(int id)
        {
            var companyToBeDeleted = _unitOfWork.Company.Get(u=>u.Id == id);
            if (companyToBeDeleted == null)
            {
                return Json(new {success = false, message = "Error while deleting"});
            }
            _unitOfWork.Company.Remove(companyToBeDeleted);
            _unitOfWork.Save();

           return Json(new { success = true, message="Delete Successful"});
        }
        #endregion
    }
}
