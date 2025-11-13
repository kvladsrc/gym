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

int main(int argc, char *argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    int n;
    std::cin >> n;
    string in;
    std::cin >> in;
    int start = 0, end = in.size() - 1;
    while (start <= end) {
      if (in[start] == '0' && in[end] == '1') {
        start++;
        end--;
        continue;
      }

      if (in[start] == '1' && in[end] == '0') {
        start++;
        end--;
        continue;
      }

      break;
    }

    std::cout << end - start + 1 << "\n";
  }
  return 0;
}
