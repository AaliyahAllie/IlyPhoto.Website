// ILY Photo - Integrated Client & Admin Scripts (Calendar, Pricing, Rescheduling, Dark Mode & Afrikaans/English Localization)

let calendarDate = new Date(new Date().getFullYear(), new Date().getMonth(), 1);

const I18N_DICTIONARY = {
    en: {
        nav_home: "Home",
        nav_about: "About & NGO",
        nav_portfolio: "Portfolio",
        nav_pricing: "Pricing",
        nav_contact: "Contact Us",
        nav_signin: "Sign In",
        nav_register: "Register",
        nav_dashboard: "Dashboard",
        nav_book: "Book Session",
        nav_gallery: "My Private Gallery",
        nav_invoices: "Invoices",
        nav_reviews: "Reviews",
        nav_profile: "Profile",
        nav_logout: "Logout",
        portal_subtitle: "CLIENT & PHOTOGRAPHER PORTAL",
        welcome_back: "Welcome Back",
        email_label: "Email Address",
        password_label: "Password",
        forgot_password: "Forgot password?",
        sign_in_btn: "Sign In",
        hero_eyebrow: "STRANDFONTEIN • CAPE TOWN PHOTOGRAPHY & COMMUNITY MEDIA STUDIO",
        view_packages: "View Packages & Rates",
        explore_portfolio: "Explore Portfolio →"
    },
    af: {
        nav_home: "Tuis",
        nav_about: "Oor Ons & NWO",
        nav_portfolio: "Portefeulje",
        nav_pricing: "Pryse & Pakkette",
        nav_contact: "Kontak Ons",
        nav_signin: "Meld Aan",
        nav_register: "Registreer",
        nav_dashboard: "Paneelbord",
        nav_book: "Bespreek Sessie",
        nav_gallery: "Privaat Galery",
        nav_invoices: "Fakture",
        nav_reviews: "Resensies",
        nav_profile: "Profiel",
        nav_logout: "Teken Uit",
        portal_subtitle: "KLIËNT & FOTOGRAAF PORTAAL",
        welcome_back: "Welkom Terug",
        email_label: "E-posadres",
        password_label: "Wagwoord",
        forgot_password: "Wagwoord vergeet?",
        sign_in_btn: "Meld Aan",
        hero_eyebrow: "STRANDFONTEIN • KAAPSTAD FOTOGRAFIE & GEMEENSKAPSMEDIA-ATELJEE",
        view_packages: "Bekyk Pakkette & Tariewe",
        explore_portfolio: "Verken Portefeulje →"
    }
};

function applyTheme(dark) {
    if (dark) {
        document.body.classList.add('dark-mode');
    } else {
        document.body.classList.remove('dark-mode');
    }
    document.querySelectorAll('.theme-toggle-btn').forEach(btn => {
        btn.textContent = dark ? '☀️ Light' : '🌙 Dark';
    });
}

function toggleDarkMode() {
    const isDark = !document.body.classList.contains('dark-mode');
    localStorage.setItem('ily_theme', isDark ? 'dark' : 'light');
    applyTheme(isDark);
}

function setLanguage(lang) {
    const selected = (lang === 'af') ? 'af' : 'en';
    localStorage.setItem('ily_lang', selected);
    document.documentElement.lang = selected;
    document.querySelectorAll('.lang-selector').forEach(sel => {
        sel.value = selected;
    });
    const dict = I18N_DICTIONARY[selected] || I18N_DICTIONARY.en;
    document.querySelectorAll('[data-i18n]').forEach(el => {
        const key = el.getAttribute('data-i18n');
        if (key && dict[key]) {
            el.textContent = dict[key];
        }
    });
}

document.addEventListener('DOMContentLoaded', () => {
    const savedTheme = localStorage.getItem('ily_theme');
    applyTheme(savedTheme === 'dark');
    const savedLang = localStorage.getItem('ily_lang') || 'en';
    setLanguage(savedLang);
});

function initBookingCalendar(admin = false) {
    window.calendarAdmin = admin;
    renderCalendar();
}

function changeMonth(n) {
    calendarDate.setMonth(calendarDate.getMonth() + n);
    renderCalendar();
}

async function renderCalendar() {
    const el = document.getElementById('calendar');
    if (!el) return;
    const y = calendarDate.getFullYear(), m = calendarDate.getMonth();
    const titleEl = document.getElementById('calendarTitle');
    if (titleEl) {
        titleEl.textContent = calendarDate.toLocaleDateString('en-ZA', { month: 'long', year: 'numeric' });
    }

    const noticeEl = document.getElementById('calendarStatusNotice');
    let status = {};
    try {
        const res = await fetch(`/api/calendar?year=${y}&month=${m + 1}`, { credentials: 'same-origin' });
        if (res.ok) {
            status = await res.json();
            if (noticeEl) noticeEl.style.display = 'none';
        } else if (noticeEl) {
            noticeEl.textContent = 'Live calendar status could not be refreshed. Availability will be verified on the server when you submit.';
            noticeEl.style.display = 'block';
        }
    } catch (e) {
        if (noticeEl) {
            noticeEl.textContent = 'Offline / network delay: live calendar status could not be loaded. Server-side conflict checks remain active.';
            noticeEl.style.display = 'block';
        }
    }
    if (calendarDate.getFullYear() !== y || calendarDate.getMonth() !== m) return;

    const first = new Date(y, m, 1).getDay();
    const days = new Date(y, m + 1, 0).getDate();
    const names = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'];
    const today = new Date();
    today.setHours(0, 0, 0, 0);

    let html = names.map(x => `<div class="cal-name">${x}</div>`).join('');
    for (let i = 0; i < first; i++) html += '<div class="cal-cell empty"></div>';

    for (let d = 1; d <= days; d++) {
        let cls = status[d] || 'available';
        const isPast = new Date(y, m, d) < today;
        if (isPast && !window.calendarAdmin && cls === 'available') cls = 'blocked';
        const label = cls === 'busy' ? (window.calendarAdmin ? 'Booked' : 'Busy')
            : cls === 'mine' ? 'Your event'
            : cls === 'blocked' ? (isPast && !status[d] ? 'Past' : 'Blocked')
            : 'Available';

        html += `<button type="button" class="cal-cell ${cls}" onclick="selectDate(${y},${m},${d},'${cls}')"><b>${d}</b><span>${label}</span></button>`;
    }
    el.innerHTML = html;
}

function selectDate(y, m, d, status) {
    const mm = String(m + 1).padStart(2, '0'), dd = String(d).padStart(2, '0');
    const iso = `${y}-${mm}-${dd}`;
    if (window.calendarAdmin) {
        if (status === 'blocked') {
            if (confirm(`Make ${iso} available again?`)) {
                const unblockInput = document.getElementById('unblockDate');
                if (unblockInput) {
                    unblockInput.value = iso;
                    document.getElementById('unblockForm')?.submit();
                }
            }
        } else if (status === 'available') {
            const blockInput = document.getElementById('blockDate');
            if (blockInput) {
                blockInput.value = iso;
                document.getElementById('blockDialog')?.showModal();
            }
        }
        return;
    }

    if (status === 'busy' || status === 'blocked') return;
    const input = document.getElementById('bookingDate');
    if (input) input.value = iso;
}

// ---- Booking price calculator ----
function initPriceCalculator() {
    ['eventType', 'startTime', 'endTime'].forEach(id => {
        const el = document.getElementById(id);
        if (!el) return;
        el.addEventListener('change', calcPrice);
        el.addEventListener('input', calcPrice);
    });
    calcPrice();
}

function calcHours() {
    const s = document.getElementById('startTime')?.value, e = document.getElementById('endTime')?.value;
    if (!s || !e) return null;
    const [sh, sm] = s.split(':').map(Number), [eh, em] = e.split(':').map(Number);
    const mins = (eh * 60 + em) - (sh * 60 + sm);
    return mins > 0 ? Math.max(1, Math.ceil(mins / 60)) : null;
}

function calcPrice() {
    const hours = calcHours();
    const hoursInput = document.getElementById('hours');
    if (hoursInput && hours) hoursInput.value = hours;
    const type = document.getElementById('eventType')?.value;
    const svc = (window.servicePricing || []).find(s => s.name === type);
    let price = 0;
    if (svc && hours) {
        price = svc.hourlyOnly
            ? hours * svc.basePrice
            : svc.basePrice + Math.max(0, hours - svc.includedHours) * svc.extraHourPrice;
    }
    const out = document.getElementById('priceEstimate');
    if (out) out.textContent = 'R' + price.toLocaleString('en-ZA');
}

// ---- Admin helpers ----
function openReschedule(btn) {
    document.getElementById('reId').value = btn.dataset.id || '';
    document.getElementById('reDate').value = btn.dataset.date || '';
    document.getElementById('reStart').value = btn.dataset.start || '10:00';
    document.getElementById('reEnd').value = btn.dataset.end || '12:00';
    const locEl = document.getElementById('reLocation');
    if (locEl) locEl.value = btn.dataset.location || '';
    const notesEl = document.getElementById('reNotes');
    if (notesEl) notesEl.value = btn.dataset.notes || '';
    document.getElementById('rescheduleDialog').showModal();
}

function initClientBookingPicker(clientId, bookingId, onPick) {
    const client = document.getElementById(clientId), booking = document.getElementById(bookingId);
    if (!client || !booking) return;
    const all = Array.from(booking.options).slice(1);
    function refill() {
        booking.length = 1;
        all.filter(o => o.dataset.client === client.value).forEach(o => booking.add(o.cloneNode(true)));
    }
    client.addEventListener('change', refill);
    booking.addEventListener('change', () => onPick && onPick(booking.selectedOptions[0]));
    refill();
}

// Print single invoice
function printInvoice(id) {
    const dlg = document.getElementById(id);
    if (!dlg) return;
    dlg.classList.add('printing');
    document.body.classList.add('printing-invoice');
    const cleanup = () => {
        dlg.classList.remove('printing');
        document.body.classList.remove('printing-invoice');
        window.removeEventListener('afterprint', cleanup);
    };
    window.addEventListener('afterprint', cleanup);
    window.print();
}

// Global Image Lightbox Modal Viewer
function openLightbox(url, caption) {
    const modal = document.getElementById('imageLightboxModal');
    const target = document.getElementById('lightboxTargetImg');
    const cap = document.getElementById('lightboxCaption');
    if (modal && target) {
        target.src = url;
        if (cap) cap.textContent = caption || '';
        modal.style.display = 'flex';
    }
}

function closeLightbox(e) {
    if (e && e.target && e.target.id === 'lightboxTargetImg') return;
    const modal = document.getElementById('imageLightboxModal');
    if (modal) modal.style.display = 'none';
}
