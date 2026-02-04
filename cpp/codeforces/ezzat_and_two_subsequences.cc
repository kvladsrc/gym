#include <algorithm>
#include <cstdint>
#include <cstdlib>
#include <ctime>
#include <iomanip>
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
    vector<int> a(n);
    int max_val = -1000000000;
    double sum = 0;
    for (auto& i : a) {
      std::cin >> i;
      sum += i;
      if (i > max_val) max_val = i;
    }
    sum -= max_val;
    std::cout << std::fixed << std::setprecision(9) << sum / (n - 1) + max_val
              << "\n";
  }

  return 0;
}
