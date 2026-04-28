function updateGlobalBadges() {
    try {
        var cart = JSON.parse(localStorage.getItem('artify_cart') || '[]');
        var wishlist = JSON.parse(localStorage.getItem('artify_wishlist') || '[]');
        
        var cartSize = cart.length;
        var wishSize = wishlist.length;

        // Elements across different navbar instances
        var cartBadges = document.querySelectorAll('#cartBadge, .nav-badge[id="cartBadge"]');
        var wishBadges = document.querySelectorAll('#wishBadge, .nav-badge[id="wishBadge"], #wishlistBadge');

        cartBadges.forEach(function(cb) {
            cb.textContent = cartSize;
            cb.style.display = cartSize > 0 ? 'grid' : 'none';
            // Explicitly set matching blue badge style
            cb.style.background = '#6B9BE6'; 
            cb.style.color = '#fff';
            // some pages use class list for zero
            if(cb.classList.contains('zero') && cartSize > 0) cb.classList.remove('zero');
            else if(!cb.classList.contains('zero') && cartSize === 0) cb.classList.add('zero');
        });

        wishBadges.forEach(function(wb) {
            wb.textContent = wishSize;
            wb.style.display = wishSize > 0 ? 'grid' : 'none';
            // Explicitly set matching orange badge style
            wb.style.background = '#EDA312';
            wb.style.color = '#fff';
            // some pages use class list for zero
            if(wb.classList.contains('zero') && wishSize > 0) wb.classList.remove('zero');
            else if(!wb.classList.contains('zero') && wishSize === 0) wb.classList.add('zero');
        });
    } catch(e) {
        console.error("Error updating badges: ", e);
    }
}

document.addEventListener('DOMContentLoaded', updateGlobalBadges);
// Optionally listen for storage events to update immediately across tabs
window.addEventListener('storage', function(e) {
    if (e.key === 'artify_cart' || e.key === 'artify_wishlist') {
        updateGlobalBadges();
    }
});

// Since the badge script relies on local function calls in pages like Wishlist/Cart/ArtworkDetail,
// we alias `updateBadges` to our new `updateGlobalBadges` so we don't break their scripts
window.updateBadges = updateGlobalBadges;
