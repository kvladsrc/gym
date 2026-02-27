#include "cpp/warmup/black_white_array.hpp"

#include <algorithm>
#include <climits>
#include <cstddef>

bool bwarray::segment_used(int seg) { return (total >> seg) & 1; }

std::size_t bwarray::start_idx(int seg) { return (1 << seg) - 1; }

std::size_t bwarray::end_idx(int seg) { return start_idx(seg + 1) - 1; }

void bwarray::insert(int el) {
  black.front() = {el, false};
  merge(0);
  total++;
}

void bwarray::merge(int seg) {
  auto start = start_idx(seg);
  auto end = end_idx(seg);

  // Incert black i segment to white i segment.
  if (!segment_used(seg)) {
    if (white.size() < end + 1) {
      white.resize(end + 1);
      lazy.resize(seg + 1);
    }

    lazy[seg] = 0;
    while (start <= end) {
      white[start] = black[start];
      if (white[start].deleted) {
        lazy[seg]++;
      }
      start++;
    }

    return;
  }

  // Merge white and black i segmets to black i + 1 segment.
  if (black.size() < end_idx(seg + 1) + 1) {
    black.resize(end_idx(seg + 1) + 1);
  }

  auto w_start = start;
  auto i = start_idx(seg + 1);
  lazy[seg] = 0;

  while (start <= end || w_start <= end) {
    if (start > end) {
      black[i++] = white[w_start++];
      continue;
    }

    if (w_start > end) {
      black[i++] = black[start++];
      continue;
    }

    if (black[start] < white[w_start]) {
      black[i++] = black[start++];
    } else {
      black[i++] = white[w_start++];
    }
  }

  // Recursivle continue until elements wont be merged to white.
  merge(seg + 1);
}

bool bwarray::search(int el) {
  for (int seg = 0; start_idx(seg) < total; ++seg) {
    if (segment_used(seg)) {
      auto first = white.begin() + start_idx(seg);
      auto last = white.begin() + end_idx(seg) + 1;

      if (el < (*first).val || el > (*(white.begin() + end_idx(seg))).val) {
        continue;
      }

      auto it = std::lower_bound(first, last, elem{el});

      while (it != last && (*it).val == el) {
        if (!(*it).deleted) {
          return true;
        }
        ++it;
      }
    }
  }

  return false;
}

void bwarray::remove(int el) {
  for (int seg = 0; start_idx(seg) < total; ++seg) {
    if (segment_used(seg)) {
      auto first = white.begin() + start_idx(seg);
      auto last = white.begin() + end_idx(seg) + 1;

      if (el < (*first).val || el > (*(white.begin() + end_idx(seg))).val) {
        continue;
      }

      auto it = std::lower_bound(first, last, elem{el});

      while (it != last && (*it).val == el) {
        if (!(*it).deleted) {
          (*it).deleted = true;
          lazy[seg]++;
          total_lazy++;
          fix(seg);
          return;
        }
        ++it;
      }
    }
  }
}

void bwarray::fix(int seg) {
  if (seg == 0) {
    total--;
    total_lazy--;
    lazy.front() = 0;
    return;
  }

  if ((lazy[seg] * 2) == (1 << seg)) {
    auto i = start_idx(seg);
    auto end = end_idx(seg);
    auto j = start_idx(seg - 1);

    // Move all non-deleted elements from seg to black segment - 1
    // because half of seg elemnts are deleted.
    total -= 1 << seg;
    total_lazy -= lazy[seg];
    lazy[seg] = 0;

    while (i <= end) {
      if (!white[i].deleted) {
        black[j++] = white[i];
      }
      i++;
    }

    merge(seg - 1);
    total += 1 << (seg - 1);
  }
}
