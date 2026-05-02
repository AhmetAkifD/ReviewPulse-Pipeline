from selenium import webdriver
from selenium.webdriver.common.by import By
from selenium.webdriver.chrome.options import Options
from selenium.webdriver.chrome.service import Service
from webdriver_manager.chrome import ChromeDriverManager
from selenium.webdriver.support.ui import WebDriverWait
from selenium.webdriver.support import expected_conditions as EC

def get_product_name(url):
    options = Options()
    options.add_argument('--headless')
    options.add_argument('--disable-gpu')
    options.add_argument('--no-sandbox')
    options.add_argument('--disable-dev-shm-usage')
    
    # Hepsiburada gibi sitelerde bot algılamasını aşmak için user-agent
    options.add_argument('user-agent=Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/114.0.0.0 Safari/537.36')
    
    driver = None
    try:
        service = Service(ChromeDriverManager().install())
        driver = webdriver.Chrome(service=service, options=options)
        driver.get(url)
        
        # Ürün isminin bulunduğu sınıfın yüklenmesini bekle
        element = WebDriverWait(driver, 10).until(
            EC.presence_of_element_located((By.CLASS_NAME, "xeL9CQ3JILmYoQPCgDcl"))
        )
        return element.text.strip()
    except Exception as e:
        raise Exception(f"Ürün adı çekilirken hata oluştu. Linki kontrol edin veya daha sonra tekrar deneyin. Detay: {str(e)}")
    finally:
        if driver:
            driver.quit()
