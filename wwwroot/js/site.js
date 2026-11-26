const menuToggle = document.querySelector('.menu-toggle');
const categoryMenu = document.querySelector('#category-menu');

if (menuToggle && categoryMenu) {
    menuToggle.addEventListener('click', () => {
        const isExpanded = menuToggle.getAttribute('aria-expanded') === 'true';
        menuToggle.setAttribute('aria-expanded', String(!isExpanded));
        menuToggle.setAttribute('aria-label', isExpanded ? '메뉴 열기' : '메뉴 닫기');
        categoryMenu.classList.toggle('is-open', !isExpanded);
    });

    categoryMenu.addEventListener('click', (event) => {
        if (event.target instanceof HTMLAnchorElement) {
            menuToggle.setAttribute('aria-expanded', 'false');
            menuToggle.setAttribute('aria-label', '메뉴 열기');
            categoryMenu.classList.remove('is-open');
        }
    });
}

const categoryButtons = document.querySelectorAll('.category-card[aria-controls]');

categoryButtons.forEach((button) => {
    button.addEventListener('click', () => {
        const panelId = button.getAttribute('aria-controls');
        const panel = panelId ? document.getElementById(panelId) : null;
        const isExpanded = button.getAttribute('aria-expanded') === 'true';

        categoryButtons.forEach((otherButton) => {
            const otherPanelId = otherButton.getAttribute('aria-controls');
            const otherPanel = otherPanelId ? document.getElementById(otherPanelId) : null;
            otherButton.setAttribute('aria-expanded', 'false');
            if (otherPanel) {
                otherPanel.hidden = true;
            }
        });

        if (!isExpanded && panel) {
            button.setAttribute('aria-expanded', 'true');
            panel.hidden = false;
        }
    });
});
