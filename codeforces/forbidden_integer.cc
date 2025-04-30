#include <algorithm>
#include <climits>
#include <cmath>
#include <cstdint>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <map>
#include <memory>
#include <numeric>
#include <queue>
#include <set>
#include <stack>
#include <string>
#include <unordered_map>
#include <vector>

using std::abs;
using std::endl;
using std::map;
using std::max;
using std::min;
using std::pair;
using std::priority_queue;
using std::queue;
using std::set;
using std::sort;
using std::sqrt;
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

int main(int /*argc*/, char* /*argv*/[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  int t = 0;
  std::cin >> t;
  while ((t--) != 0) {
    int n = 0;
    int k = 0;
    int x = 0;
    std::cin >> n >> k >> x;

    if (x != 1) {
      std::cout << "YES" << "\n";
      std::cout << n << "\n";
      for (int i = 0; i < n; ++i) {
        std::cout << 1 << " ";
      }
      std::cout << "\n";
      continue;
    }

    if (n % 2 == 0) {
      if (k >= 2) {
        std::cout << "YES" << "\n";
        std::cout << n / 2 << "\n";
        while (n > 0) {
          std::cout << 2 << " ";
          n -= 2;
        }
        std::cout << "\n";
      } else {
        std::cout << "NO" << "\n";
      }
      continue;
    }

    if (n == 1) {
      std::cout << "NO" << "\n";
      continue;
    }

    if (k >= 3) {
      std::cout << "YES" << "\n";
      std::cout << 1 + ((n - 3) / 2) << "\n";
      std::cout << 3 << " ";
      n -= 3;
      while (n > 0) {
        std::cout << 2 << " ";
        n -= 2;
      }
      std::cout << "\n";
      continue;
    }

    std::cout << "NO" << "\n";
  }

  return 0;
}
