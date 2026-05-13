window.downloadTextFile = function (fileName, text) {
  const blob = new Blob([text], { type: 'application/json;charset=utf-8' });
  const url = URL.createObjectURL(blob);
  const anchor = document.createElement('a');
  anchor.href = url;
  anchor.download = fileName;
  anchor.rel = 'noopener';
  anchor.click();
  URL.revokeObjectURL(url);
};

window.migratorLocalStorage = {
  getItem: function (key) {
    return localStorage.getItem(key);
  },
  setItem: function (key, value) {
    localStorage.setItem(key, value);
  },
  removeItem: function (key) {
    localStorage.removeItem(key);
  }
};
