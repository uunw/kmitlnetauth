https://github.com/CE-HOUSE/Auto-Authen-KMITL
จาก repo ดังกล่าวฉันต้องการสร้างใหม่โดยใช้ rust และ ต้องการให้ใช้ได้หลาย platform เช่น windows, linux, docker

โดย windows ฉันต้องการให้มีเป็นตัว installer โดยให้เลือกว่าต้องการเลือกแบบ all user หรือ local user
service จะ run เป็น backgound service และมี menu icon ให้คลิ็กขวาตั้งค่าได้

ส่วนใน linux จะมี 2 platform หลักๆคือ 1. debian base 2. rpm base
มีเป็น file .deb,.rpm ให้ติดตั้งได้เหมือนกัน และเก็บไฟล์ config ไว้ที่ /etc/kmitlnetauth/confg.conf
run เป็น backgound service เหมือนกัน (systemd)

docker เหมือนกับ linux แต่ใช้ alpine เพื่อขนาดที่เล็กลง
