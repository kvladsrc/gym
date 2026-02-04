#include <algorithm>
#include <iostream>
#include <string>
#include <unordered_map>
#include <vector>

using std::max;
using std::min;
using std::string;
using std::unordered_map;
using std::vector;

int damaged_dragons(int k, int l, int m, int n, int d) {
  int res = 0;
  for (int i = 1; i <= d; ++i) {
    if (!(i % k)) {
      res++;
      continue;
    }

    if (!(i % l)) {
      res++;
      continue;
    }

    if (!(i % m)) {
      res++;
      continue;
    }

    if (!(i % n)) {
      res++;
      continue;
    }
  }

  return res;
}

int main(int argc, char* argv[]) {
  int k, l, m, n, d;
  std::cin >> k >> l >> m >> n >> d;
  std::cout << damaged_dragons(k, l, m, n, d) << "\n";

  return 0;
}
