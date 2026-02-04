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

int main(int argc, char* argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    int n;
    std::cin >> n;

    vector<int> even_bags;
    vector<int> odd_bags;
    for (int i = 0; i < n; ++i) {
      int buf;
      std::cin >> buf;
      if (buf % 2) {
        odd_bags.push_back(buf);
      } else {
        even_bags.push_back(buf);
      }
    }

    if (even_bags.empty()) {
      std::cout << "NO"
                << "\n";
      continue;
    }

    int a_points = even_bags[0], b_points = 0;
    int a_i = 1, b_i = 0;
    bool prop = true;
    while (b_i < static_cast<int>(odd_bags.size())) {
      if (a_points <= b_points + odd_bags[b_i]) {
        if (a_i >= static_cast<int>(even_bags.size())) {
          prop = false;
          break;
        }
        a_points += even_bags[a_i++];
      } else {
        b_points += odd_bags[b_i++];
      }
    }

    if (prop) {
      std::cout << "YES"
                << "\n";
    } else {
      std::cout << "NO"
                << "\n";
    }
  }

  return 0;
}
