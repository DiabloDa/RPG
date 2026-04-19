# RPG
#### Entrega número 3
- Jorge Calle Moreno
- Luisa Fernanda Buelvas
- Juliana Monroy Andrae
- Jesús Bonivento

#### Descripción 

El trabajo esta basado en un ARPG, enfocado en ataques direccionales, integrando locomotion, Shake de cámara, un control de estados, la posiblidad de recibir daño y de morir por un elemento externo.

#### Algunos criterios de diseño:

El sistema se encarga de analizar los movimientos del joystick; primero, ignora movimientos accidentales; segundo, memoriza la dirección un breve momento para no perder el input; y tercero, usa un sistema de prioridades para decidir hacia dónde atacar durante un combo, garantizando que el control sea fluido y no dé giros bruscos.

#### Locomotion: 

Se busco generar transiciones limpias en los movimientos, integrando de manera limpia los movimientos y rotaciones de sus desplazamientos. 
Cuando el personaje esta atacando, se bloquean los otros movimientos, esto con el fin de mantener un flujo limpio y no interrumpir los ataques. También la cámara se mantiene por detrás del personaje para seguirlo y tener un buen angulo.

#### Feedback Impacto (Camera Shake):

Para cada ataque se busco generar un tipo de feedback diferente. 
Para el ataque que carga lentameente la cámara sube despacio para luego caer con más fuerza y generar ese impacto fuerte.
Para el ataque rápido, la cámara sube y baja de forma más rápida y suave, esto con el fin de manteneruna suavidad en este ataque que es más suave.

#### Limitaciones:

Algunos bugs que tenemos y complicaciones que se nos presentaron fueron:

- A veces el personaje se traba con el bloque que le hace daño, y al levantarse se pone en este mismo bloque y vuelve a recibir daño.
- El feedback de la cámara con el combo falla, porque a veces trata de hacer el ataque de carga lento y falla.

#### Enlace:

- https://drive.google.com/drive/u/1/folders/10JAKUaogU4VWTRHtpQuP6TfI6MPbpPlV
