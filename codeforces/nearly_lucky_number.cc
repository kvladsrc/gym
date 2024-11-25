#include <iostream>

bool is_lucky(int n) {
  while (n > 9) {
    if (n % 10 == 4 || n % 10 == 7) {
      n /= 10;
      continue;
    }

    return false;
  }

  return (n == 4 || n == 7);
}

bool is_near_lucky(long long n) {
  int four = 0, seven = 0;

  while (n > 9) {
    switch (n % 10) {
      case 4:
        four++;
        break;
      case 7:
        seven++;
        break;
    }
    n /= 10;
  }

  if (n == 4) {
    four++;
  }

  if (n == 7) {
    seven++;
  }

  return is_lucky(four + seven);
}

int main(int argc, char *argv[]) {
  long long input;
  std::cin >> input;
  if (is_near_lucky(input)) {
    std::cout << "YES"
              << "\n";
  } else {
    std::cout << "NO"
              << "\n";
  }

  return 0;
}
