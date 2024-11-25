#include <iostream>

struct vec {
  int x, y, z;

  vec operator+(const vec &other) {
    vec res;
    res.x = x + other.x;
    res.y = y + other.y;
    res.z = z + other.z;
    return res;
  }

  bool is_zero() { return (!x && !y && !z); }
};

int main(int argc, char *argv[]) {
  int n;
  std::cin >> n;
  vec acc = {0, 0, 0};
  for (int i = 0; i < n; ++i) {
    vec buf;
    std::cin >> buf.x >> buf.y >> buf.z;
    acc = acc + buf;
  }
  if (acc.is_zero())
    std::cout << "YES"
              << "\n";
  else
    std::cout << "NO"
              << "\n";

  return 0;
}
