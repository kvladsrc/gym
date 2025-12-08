#include <algorithm>
#include <climits>
#include <cmath>
#include <cstdint>
#include <cstdlib>
#include <ctime>
#include <functional>
#include <iostream>
#include <map>
#include <memory>
#include <numeric>
#include <optional>
#include <queue>
#include <set>
#include <stack>
#include <string>
#include <tuple>
#include <unordered_map>
#include <utility>
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
    std::cin >> n;
    for (int i = 0; i < n; ++i) {
      int buf = 0;
      std::cin >> buf;
    }

    if (n % 2 == 0) {
      std::cout << 2 << "\n";
      std::cout << 1 << " " << n << "\n";
      std::cout << 1 << " " << n << "\n";
    } else {
      std::cout << 4 << "\n";
      std::cout << 1 << " " << n - 1 << "\n";
      std::cout << 1 << " " << n - 1 << "\n";
      std::cout << n - 1 << " " << n << "\n";
      std::cout << n - 1 << " " << n << "\n";
    }
  }

  return 0;
}
