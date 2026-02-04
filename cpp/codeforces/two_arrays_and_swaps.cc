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
    int n, k;
    std::cin >> n >> k;

    int first_sum = 0;

    vector<int> first(n);
    for (int i = 0; i < n; ++i) {
      std::cin >> first[i];
      first_sum += first[i];
    }

    vector<int> second(n);
    for (int i = 0; i < n; ++i) {
      std::cin >> second[i];
    }

    sort(first.begin(), first.end());
    sort(second.begin(), second.end());

    int first_pos = 0, second_pos = n - 1;
    while (k--) {
      if (first[first_pos] < second[second_pos]) {
        first_sum -= first[first_pos++];
        first_sum += second[second_pos--];
      }
    }

    std::cout << first_sum << "\n";
  }
  return 0;
}
