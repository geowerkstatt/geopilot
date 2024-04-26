import "../../app.css";
import { Carousel } from "react-bootstrap";

export const InfoCarousel = ({ content }) => (
  <Carousel interval={null} nextLabel="" prevLabel="">
    {content?.split("\n").map(item => (
      <Carousel.Item key={item}>
        <div>{item}</div>
      </Carousel.Item>
    ))}
  </Carousel>
);

export default InfoCarousel;
