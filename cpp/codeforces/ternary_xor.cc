#include <algorithm>
#include <cstdlib>
#include <ctime>
#include <iostream>
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

int main(int argc, char* argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    int n;
    std::cin >> n;
    vector<int> x(n);
    for (int i = 0; i < n; ++i) {
      char c;
      std::cin >> c;
      x[i] = c - '0';
    }

    vector<int> a(n);
    vector<int> b(n);
    a[0] = b[0] = 1;
    bool a_is_bigger = false;
    for (int i = 1; i < n; ++i) {
      if (a_is_bigger) {
        a[i] = 0;
        b[i] = x[i];
        continue;
      }

      switch (x[i]) {
        case 0:
          a[i] = b[i] = 0;
          break;
        case 1:
          a[i] = 1;
          b[i] = 0;
          a_is_bigger = true;
          break;
        default:
          a[i] = b[i] = 1;
      }
    }

    for (int i : a) {
      std::cout << i;
    }
    std::cout << "\n";

    for (int i : b) {
      std::cout << i;
    }
    std::cout << "\n";
  }

  return 0;
}
