#include <iostream>

bool is_lucky(int a) {
  if (!a) return false;

  while (a) {
    switch (a % 10) {
      case 4:
        break;
      case 7:
        break;
      default:
        return false;
    }
    a /= 10;
  }
  return true;
}

int main(int argc, char* argv[]) {
  int n;
  std::cin >> n;
  bool found = false;
  for (int i = 1; i <= n / 2; ++i) {
    if (!is_lucky(i)) continue;
    if (n % i == 0) {
      found = true;
      break;
    }
  }

  if (found || is_lucky(n)) {
    std::cout << "YES"
              << "\n";
  } else {
    std::cout << "NO"
              << "\n";
  }

  return 0;
}
