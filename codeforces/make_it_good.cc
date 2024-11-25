#include <algorithm>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <limits>
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
    for (auto &i : a) {
      std::cin >> i;
    }

    bool up = true;
    int pref_len = 0;

    for (int i = n - 2; i >= 0; --i) {
      if (up) {
        if (a[i] < a[i + 1]) {
          up = false;
        }
      } else {
        if (a[i] > a[i + 1]) {
          pref_len = i + 1;
          break;
        }
      }
    }

    std::cout << pref_len << "\n";
  }

  return 0;
}
