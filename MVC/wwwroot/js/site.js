// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.



function logoutArtist() {
    // 1. Clear LocalStorage
    localStorage.removeItem("authToken");
    localStorage.removeItem("userData");

    // 2. Optional: If you used Session, call the API to clear it
    $.ajax({
        url: '/Artist/ClearSession', // Route to your MVC Controller
        type: 'POST',
        success: function () {
            // 3. Redirect to Login Page
            window.location.href = "/Artist/Login";
        }
    });
}