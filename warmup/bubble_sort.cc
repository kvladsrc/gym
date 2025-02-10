#include <cstddef>
#include <iostream>
#include <vector>

using std::vector;

static void bubble_sort(vector<int> &a) {
  while (true) {
    bool swapped = false;

    for (size_t i = 1; i < a.size(); ++i) {
      if (a[i - 1] > a[i]) {
        std::swap(a[i - 1], a[i]);
        swapped = true;
      }
    }

    if (!swapped) {
      break;
    }
  }
}

int main(int /*argc*/, char * /*argv*/[]) {
  int n = 0;
  std::cin >> n;
  vector<int> a(n);
  for (auto &i : a) {
    std::cin >> i;
  }

  bubble_sort(a);

  for (auto i : a) {
    std::cout << i << " ";
  }
  std::cout << "\n";

  return 0;
}
