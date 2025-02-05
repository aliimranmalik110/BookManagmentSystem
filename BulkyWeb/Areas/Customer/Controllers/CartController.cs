using System.Security.Claims;
using BulkyBook.DataAccess.Repository;
using BulkyBook.DataAccess.Repository.IRepository;
using BulkyBook.Models;
using BulkyBook.Models.ViewModels;
using BulkyBook.Utility;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Stripe.Checkout;
using static System.Net.WebRequestMethods;

namespace BulkyBookWeb.Areas.Customer.Controllers
{
    [Area("Customer")]
    [Authorize]
    public class CartController : Controller
    {

        private readonly IUnitOfWork _unitofOfWork;
        [BindProperty]
        public ShoppingCartVM ShoppingCartVM { get; set; }

        public CartController(IUnitOfWork unitOfWork)
        {
            _unitofOfWork = unitOfWork;
        }
        public IActionResult Index()
        {

            var claimsIdentity = (ClaimsIdentity)User.Identity;
            var userId = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier).Value;


            ShoppingCartVM = new()
            {
                ShoppingCartList = _unitofOfWork.ShoppingCart.GetAll(
                    u => u.ApplicationUserId == userId, includeProperties: "Product"),
                OrderHeader = new()
            };
            //get all images
            IEnumerable<ProductImage> productImages = _unitofOfWork.ProductImage.GetAll();
           
            //calculate the cart value
            foreach (var cart in ShoppingCartVM.ShoppingCartList)
            {
                cart.Product.ProductImages = productImages.Where(u => u.ProductId == cart.Product.Id).ToList();
                cart.Price = GetPriceBasedOnQuantity(cart);
                ShoppingCartVM.OrderHeader.OrderTotal += (cart.Price * cart.Count);
            }
            return View(ShoppingCartVM);
        }

        //cart summary view
        public IActionResult Summary()
        {
            var claimsIdentity = (ClaimsIdentity)User.Identity;
            var userID = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier).Value;


            ShoppingCartVM = new()
            {
                ShoppingCartList = _unitofOfWork.ShoppingCart.GetAll(
                    u => u.ApplicationUserId == userID, includeProperties: "Product"),
                OrderHeader = new()
            };

            ShoppingCartVM.OrderHeader.ApplicationUser = _unitofOfWork.ApplicationUser.Get(u => u.Id == userID);
            //populate the shiping field based on user data
            ShoppingCartVM.OrderHeader.Name = ShoppingCartVM.OrderHeader.ApplicationUser.Name;
            ShoppingCartVM.OrderHeader.StreetAddres = ShoppingCartVM.OrderHeader.ApplicationUser.StreetAddress;
            ShoppingCartVM.OrderHeader.PhoneNumber = ShoppingCartVM.OrderHeader.ApplicationUser.PhoneNumber;
            ShoppingCartVM.OrderHeader.Country = ShoppingCartVM.OrderHeader.ApplicationUser.Country;
            ShoppingCartVM.OrderHeader.State = ShoppingCartVM.OrderHeader.ApplicationUser.State;
            ShoppingCartVM.OrderHeader.PostalCode = ShoppingCartVM.OrderHeader.ApplicationUser.PostalCode;
            ShoppingCartVM.OrderHeader.City = ShoppingCartVM.OrderHeader.ApplicationUser.City;

            //calculate the cart value
            foreach (var cart in ShoppingCartVM.ShoppingCartList)
            {
                cart.Price = GetPriceBasedOnQuantity(cart);
                ShoppingCartVM.OrderHeader.OrderTotal += (cart.Price * cart.Count);
            }
            return View(ShoppingCartVM);
        }

        //cart summary post
        [HttpPost]
        [ActionName("Summary")]
        public IActionResult SummaryPOST()
        {
            var claimsIdentity = (ClaimsIdentity)User.Identity;
            var userID = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier).Value;


            ShoppingCartVM.ShoppingCartList = _unitofOfWork.ShoppingCart.GetAll(
             u => u.ApplicationUserId == userID, includeProperties: "Product");

            ShoppingCartVM.OrderHeader.OrderDate = DateTime.Now;
            ShoppingCartVM.OrderHeader.ApplicationUserId = userID;


            ApplicationUser applicationUser = _unitofOfWork.ApplicationUser.Get(u => u.Id == userID);

            //calculate the cart value
            foreach (var cart in ShoppingCartVM.ShoppingCartList)
            {
                cart.Price = GetPriceBasedOnQuantity(cart);
                ShoppingCartVM.OrderHeader.OrderTotal += (cart.Price * cart.Count);
            }

            if (applicationUser.CompanyId.GetValueOrDefault() == 0)
            {
                //if it is a regular customer account
                ShoppingCartVM.OrderHeader.PaymentStatus = SD.PaymentStatusPending;
                ShoppingCartVM.OrderHeader.OrderStatus = SD.StatusPending;
            }
            else
            {
                //if its a company, the logic goes here
                ShoppingCartVM.OrderHeader.PaymentStatus = SD.PaymentStatusDelayedPayment;
                ShoppingCartVM.OrderHeader.OrderStatus = SD.StatusApproved;
            }
            _unitofOfWork.OrderHeader.Add(ShoppingCartVM.OrderHeader);
            _unitofOfWork.Save();

            //for order details in database

            foreach (var cart in ShoppingCartVM.ShoppingCartList)
            {
                OrderDetail orderDetails = new()
                {
                    ProductId = cart.ProductId,
                    OrderHeaderId = ShoppingCartVM.OrderHeader.Id,
                    Price = cart.Price,
                    Count = cart.Count
                };
                _unitofOfWork.OrderDetail.Add(orderDetails);
                _unitofOfWork.Save();
            }

            if (applicationUser.CompanyId.GetValueOrDefault() == 0)
            {
                //if it is a regular customer account, we need to capture payment
                //stripe logic
                var domain = "https://localhost:7241/";
                var options = new Stripe.Checkout.SessionCreateOptions
                {
                    SuccessUrl = domain + $"Customer/Cart/OrderConfirmation?id={ShoppingCartVM.OrderHeader.Id}",
                    CancelUrl = domain + "Customer/Cart/Index",
                    LineItems = new List<Stripe.Checkout.SessionLineItemOptions>(),
                    Mode = "payment",
                };
                foreach (var items in ShoppingCartVM.ShoppingCartList)
                {
                    var sessionLineItems = new SessionLineItemOptions
                    {
                        PriceData = new SessionLineItemPriceDataOptions
                        {
                            UnitAmount = (long)(items.Price * 100),//20.50 => 2050
                            Currency = "usd",
                            ProductData = new SessionLineItemPriceDataProductDataOptions
                            {
                                Name = items.Product.Title
                            }
                        },
                        Quantity = items.Count
                    };
                    options.LineItems.Add(sessionLineItems);
                }
                var service = new SessionService();
                Session session = service.Create(options);
                //update paymentintent id & session id
                _unitofOfWork.OrderHeader.UpdateStripePaymentId(ShoppingCartVM.OrderHeader.Id,session.Id, session.PaymentIntentId);
                _unitofOfWork.Save();
                Response.Headers.Add("Location",session.Url);
                return new StatusCodeResult(303);
            }

            return RedirectToAction(nameof(OrderConfirmation), new { id = ShoppingCartVM.OrderHeader.Id });
        }

        public IActionResult OrderConfirmation(int id)
        {
            
            //check if payment is successful
            OrderHeader orderHeader = _unitofOfWork.OrderHeader.Get(u=>u.Id == id, includeProperties:"ApplicationUser");
            if (orderHeader.PaymentStatus != SD.PaymentStatusDelayedPayment)
            {
                //this is a customer payment
                var service = new SessionService();
                Session session = service.Get(orderHeader.SessionId);
                if (session.PaymentStatus.ToLower() == "paid")
                {
                    //update payment intent id
                    _unitofOfWork.OrderHeader.UpdateStripePaymentId(id,
                        session.Id, session.PaymentIntentId);
                    _unitofOfWork.OrderHeader.UpdateStatus(id, SD.StatusApproved, SD.PaymentStatusApproved);
                    _unitofOfWork.Save();
                }
                HttpContext.Session.Clear();
            }
            
            //delete the shopping cart entries
            List<ShoppingCart> ShoppingCarts = _unitofOfWork.ShoppingCart
                .GetAll(u=>u.ApplicationUserId == orderHeader.ApplicationUserId).ToList();
            
            _unitofOfWork.ShoppingCart.RemoveRange(ShoppingCarts);
            _unitofOfWork.Save();
            return View(id);
        }

        //cart plus button
        public IActionResult plus(int cartId)
        {
            var cartFromDb = _unitofOfWork.ShoppingCart.Get(u => u.Id == cartId);
            cartFromDb.Count += 1;
            _unitofOfWork.ShoppingCart.Update(cartFromDb);
            _unitofOfWork.Save();
            return RedirectToAction(nameof(Index));
        }

        //cart minus button 
        public IActionResult minus(int cartId)
        {
            var cartFromDb = _unitofOfWork.ShoppingCart.Get(u => u.Id == cartId,tracked:true);
            if (cartFromDb.Count <= 1)
            {
                HttpContext.Session.SetInt32(SD.SessionCart, +_unitofOfWork.ShoppingCart.
               GetAll(u => u.ApplicationUserId == cartFromDb.ApplicationUserId).Count() - 1);
                //remove from cart
                _unitofOfWork.ShoppingCart.Remove(cartFromDb);
            }
            else
            {
                cartFromDb.Count -= 1;
                _unitofOfWork.ShoppingCart.Update(cartFromDb);
            }
            _unitofOfWork.Save();
            return RedirectToAction(nameof(Index));
        }
        //remove cart
        public IActionResult remove(int cartId)
        {
            var cartFromDb = _unitofOfWork.ShoppingCart.Get(u => u.Id == cartId, tracked:true);
            HttpContext.Session.SetInt32(SD.SessionCart, +_unitofOfWork.ShoppingCart.
                GetAll(u => u.ApplicationUserId == cartFromDb.ApplicationUserId).Count() - 1);
            _unitofOfWork.ShoppingCart.Remove(cartFromDb);
            
            _unitofOfWork.Save();
            
            return RedirectToAction(nameof(Index));
        }
        private double GetPriceBasedOnQuantity(ShoppingCart shoppingCart)
        {
            if (shoppingCart.Count <= 50)
            {
                return shoppingCart.Product.Price;
            }
            else
            {
                if (shoppingCart.Count <= 100)
                {
                    return shoppingCart.Product.Price50;
                }
                else
                {
                    return shoppingCart.Product.Price100;
                }
            }
        }
    }



}
