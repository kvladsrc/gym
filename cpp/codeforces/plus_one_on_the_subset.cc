#include <algorithm>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <set>
#include <stack>
#include <string>
#include <unordered_map>
#include <vector>

using std::abs;
using std::max;
using std::min;
using std::pair;
using std::set;
using std::sort;
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

#define MAX_VAL 1000000000

int main(int argc, char* argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    int n;
    std::cin >> n;
    int max_val = 0, min_val = MAX_VAL;
    for (int i = 0; i < n; ++i) {
      int buf;
      std::cin >> buf;
      if (buf > max_val) max_val = buf;
      if (buf < min_val) min_val = buf;
    }

    std::cout << max_val - min_val << "\n";
  }

  return 0;
}
