#include <cstdint>
#include <iostream>

int64_t even_odds(int64_t n, int64_t k) {
  int64_t first_seq = n / 2 + n % 2;

  if (k <= first_seq) {
    return k * 2 - 1;
  } else {
    return (k - first_seq) * 2;
  }
}

int main(int argc, char *argv[]) {
  int64_t n, k;
  std::cin >> n >> k;
  std::cout << even_odds(n, k) << "\n";

  return 0;
}
