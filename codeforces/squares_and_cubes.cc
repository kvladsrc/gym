#include <algorithm>
#include <cstdint>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <numeric>
#include <set>
#include <stack>
#include <string>
#include <unordered_map>
#include <vector>

using std::abs;
using std::gcd;
using std::max;
using std::min;
using std::pair;
using std::set;
using std::sort;
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

int main(int argc, char *argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    int n;
    std::cin >> n;

    int res = 0;
    unordered_map<int64_t, bool> m;
    for (int i = 1; i * i <= n; ++i) {
      int64_t square = i * i;
      res++;
      m[square] = true;
    }

    for (int i = 1; i * i * i <= n; ++i) {
      int64_t cube = i * i * i;
      if (!m[cube]) {
        res++;
        m[cube] = true;
      }
    }

    std::cout << res << "\n";
  }

  return 0;
}
