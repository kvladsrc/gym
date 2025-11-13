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

int main(int /*argc*/, char * /*argv*/[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  size_t n = 0;
  std::cin >> n;
  vector<int> a(n);
  for (auto &i : a) {
    std::cin >> i;

    while (true) {
      if (i % 2 == 0) {
        i /= 2;
        continue;
      }

      if (i % 3 == 0) {
        i /= 3;
        continue;
      }

      break;
    }
  }

  bool res =
      !std::any_of(a.begin(), a.end(), [&a](int i) { return i != a.front(); });

  std::cout << (res ? "YES" : "NO") << "\n";

  return 0;
}
