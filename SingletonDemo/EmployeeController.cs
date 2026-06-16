using Microsoft.AspNetCore.Mvc;

//namespace SingletonDemo
//{
    public class EmployeeController : Controller
    {
        private readonly IEmployeeService _service;

        public EmployeeController(IEmployeeService service)
        {
            _service = service;
        }

        public async Task<IActionResult> Index()
        {
            var employees = await _service.GetEmployeesAsync();
            return View(employees);
        }

        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Create(Employee emp)
        {
            if (ModelState.IsValid)
            {
                await _service.AddEmployeeAsync(emp);
                return RedirectToAction(nameof(Index));
            }
            return View(emp);
        }

        public async Task<IActionResult> Edit(string id)
        {
            var emp = await _service.GetEmployeeAsync(id);
            return View(emp);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(Employee emp)
        {
            await _service.UpdateEmployeeAsync(emp);
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Delete(string id)
        {
            var emp = await _service.GetEmployeeAsync(id);
            return View(emp);
        }

        [HttpPost, ActionName("Delete")]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            await _service.DeleteEmployeeAsync(id);
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Details(string id)
        {
            var emp = await _service.GetEmployeeAsync(id);
            return View(emp);
        }
    }
//}
