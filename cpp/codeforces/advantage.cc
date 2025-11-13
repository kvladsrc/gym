#include <algorithm>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <numeric>
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

    vector<int> participants(n);
    int first = 0;
    int second = 0;
    for (int i = 0; i < n; ++i) {
      std::cin >> participants[i];
      if (participants[i] > first) {
        second = first;
        first = participants[i];
        continue;
      }

      if (participants[i] > second) {
        second = participants[i];
      }
    }

    for (int i : participants) {
      if (i == first) {
        std::cout << i - second << " ";
      } else {
        std::cout << i - first << " ";
      }
    }
    std::cout << "\n";
  }

  return 0;
}
