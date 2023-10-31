import "./app.css";

export const Header = ({ clientSettings }) => (
  <header>
    <a href={clientSettings?.vendorLink} target="_blank" rel="noreferrer">
      <img
        className="vendor-logo"
        src="/logo.svg"
        alt="Vendor Logo"
        onError={(e) => {
          e.target.style.display = "none";
        }}
      />
    </a>
  </header>
);

export default Header;
