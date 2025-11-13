#include <algorithm>
#include <cstdint>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <numeric>
#include <queue>
#include <set>
#include <stack>
#include <string>
#include <unordered_map>
#include <utility>
#include <vector>

using std::abs;
using std::gcd;
using std::max;
using std::min;
using std::pair;
using std::priority_queue;
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

    vector<int> a(n);
    int64_t sum = 0;
    bool res = true;
    for (auto &i : a) {
      std::cin >> i;
      if (i == sum) {
        res = false;
      }
      sum += i;
    }

    if (res) {
      std::cout << "YES"
                << "\n";
      for (auto i : a) {
        std::cout << i << " ";
      }
      std::cout << "\n";
    } else {
      // Put maximum element to the front.
      sort(a.begin(), a.end());
      if (a.front() != a.back()) {
        std::swap(a[0], a[n - 1]);
        std::cout << "YES"
                  << "\n";
        for (auto i : a) {
          std::cout << i << " ";
        }
        std::cout << "\n";
      } else {
        std::cout << "NO"
                  << "\n";
      }
    }
  }

  return 0;
}
