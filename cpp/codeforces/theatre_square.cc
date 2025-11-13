#include <cstdint>
#include <iostream>

// O(1)
int main() {
  int64_t n, m, a;
  std::cin >> n >> m >> a;
  int64_t x = n / a;
  if (n % a) x++;
  int64_t y = m / a;
  if (m % a) y++;
  std::cout << x * y << "\n";
}
