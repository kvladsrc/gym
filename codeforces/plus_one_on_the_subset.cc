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

int main(int argc, char *argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    int n;
    std::cin >> n;
    int max = 0, min = MAX_VAL;
    for (int i = 0; i < n; ++i) {
      int buf;
      std::cin >> buf;
      if (buf > max) max = buf;
      if (buf < min) min = buf;
    }

    std::cout << max - min << "\n";
  }

  return 0;
}
