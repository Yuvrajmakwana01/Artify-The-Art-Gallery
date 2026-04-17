// ============================================================
//  wwwroot/js/authHelper.js  — FINAL WORKING VERSION
// ============================================================

(function () {
    "use strict";

    window.AuthHelper = {
        TOKEN_KEY: "authToken",
        USERDATA_KEY: "userData",
        API_BASE: "http://localhost:5183/api/ArtistApi",

        getToken: function () {
            return localStorage.getItem(this.TOKEN_KEY);
        },

        getUser: function () {
            try {
                var raw = localStorage.getItem(this.USERDATA_KEY);
                return raw ? JSON.parse(raw) : null;
            } catch (e) {
                return null;
            }
        },

        getArtistId: function () {
            var u = this.getUser();
            return (u && u.c_User_Id) ? parseInt(u.c_User_Id) : 0;
        },

        isAuthenticated: function () {
            var token = this.getToken();
            if (!token) return false;
            try {
                var b64 = token.split(".")[1].replace(/-/g, "+").replace(/_/g, "/");
                var payload = JSON.parse(atob(b64));
                return payload.exp > (Date.now() / 1000);
            } catch (e) {
                return false;
            }
        },

        logout: function () {
            var token = this.getToken();
            localStorage.removeItem(this.TOKEN_KEY);
            localStorage.removeItem(this.USERDATA_KEY);
            
            // Call logout API
            fetch(this.API_BASE + "/Logout", {
                method: "POST",
                headers: {
                    "Authorization": "Bearer " + (token || "")
                }
            }).finally(function () {
                window.location.href = "/Artist/Login";
            });
        },

        requireAuth: function () {
            if (!this.isAuthenticated()) {
                this.logout();
                return false;
            }
            return true;
        },

        // Helper method for making authenticated API calls
        apiCall: function (url, method, data, isFormData) {
            var options = {
                method: method || "GET",
                headers: {}
            };
            
            var token = this.getToken();
            if (token) {
                options.headers["Authorization"] = "Bearer " + token;
            }
            
            if (data) {
                if (isFormData) {
                    options.body = data;
                } else {
                    options.headers["Content-Type"] = "application/json";
                    options.body = JSON.stringify(data);
                }
            }
            
            return fetch(this.API_BASE + url, options);
        }
    };

    // jQuery AJAX setup - only attach Authorization header
    if (typeof $ !== "undefined") {
        $.ajaxSetup({
            beforeSend: function (xhr) {
                var token = AuthHelper.getToken();
                if (token) {
                    xhr.setRequestHeader("Authorization", "Bearer " + token);
                }
            }
        });

        // Global error handler
        $(document).ajaxError(function (event, xhr) {
            if (xhr.status === 401) {
                if (typeof Swal !== "undefined") {
                    Swal.fire({
                        icon: "warning",
                        title: "Session Expired",
                        text: "Please sign in again.",
                        confirmButtonColor: "#14122a"
                    }).then(function () {
                        AuthHelper.logout();
                    });
                } else {
                    AuthHelper.logout();
                }
            }
        });
    }
})();