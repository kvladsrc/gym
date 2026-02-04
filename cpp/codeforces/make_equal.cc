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

    int sum = 0;
    vector<int> a(n);
    for (int i = 0; i < n; ++i) {
      std::cin >> a[i];
      sum += a[i];
    }

    int av = sum / n;
    int buf = 0;
    bool res = true;
    for (int i : a) {
      if (i + buf < av) {
        res = false;
        break;
      }

      buf = (i + buf) - av;
    }

    if (res) {
      std::cout << "YES"
                << "\n";
    } else {
      std::cout << "NO"
                << "\n";
    }
  }

  return 0;
}
