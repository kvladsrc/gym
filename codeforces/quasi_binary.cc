#include <algorithm>
#include <climits>
#include <cmath>
#include <cstddef>
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
using std::gcd;
using std::map;
using std::max;
using std::min;
using std::pair;
using std::priority_queue;
using std::set;
using std::sort;
using std::sqrt;
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

int main(int argc, char *argv[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  int n;
  std::cin >> n;

  // Greedy.
  vector<int> a;
  while (n) {
    a.push_back(n % 10);
    n /= 10;
  }
  std::reverse(a.begin(), a.end());
  vector<int> res;
  while (true) {
    int local = 0;
    for (size_t i = 0; i < a.size(); ++i) {
      local *= 10;
      if (a[i]) {
        local += 1;
        a[i]--;
      }
    }
    if (!local) {
      break;
    } else {
      res.push_back(local);
    }
  }

  std::cout << res.size() << "\n";
  for (auto i : res) {
    std::cout << i << " ";
  }
  std::cout << "\n";

  return 0;
}
