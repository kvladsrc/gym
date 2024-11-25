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
  int t;
  std::cin >> t;
  while (t--) {
    int n;
    std::cin >> n;

    vector<int> a(n);
    for (auto &i : a) {
      std::cin >> i;
    }

    vector<int> b(n);
    for (auto &i : b) {
      std::cin >> i;
    }

    // a_u - a_v >= b_u - b_v
    // a_u - b_u >= a_v - b_v
    for (int i = 0; i < n; ++i) {
      a[i] -= b[i];
    }

    int largest = INT_MIN;
    vector<int> largest_idx;
    for (int i = 0; i < n; ++i) {
      if (a[i] > largest) {
        largest = a[i];
        largest_idx.clear();
        largest_idx.push_back(i + 1);
      } else if (a[i] == largest) {
        largest_idx.push_back(i + 1);
      }
    }

    std::cout << largest_idx.size() << "\n";
    for (auto i : largest_idx) {
      std::cout << i << " ";
    }
    std::cout << "\n";
  }

  return 0;
}
